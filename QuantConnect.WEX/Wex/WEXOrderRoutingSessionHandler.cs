using QuantConnect.Orders;
using QuantConnect.Util;
using QuantConnect.WEX.Fix;
using QuantConnect.WEX.Fix.Core;
using QuantConnect.WEX.Fix.Protocol;
using QuantConnect.WEX.Fix.Utils;
using QuickFix;
using QuickFix.Fields;
using QuickFix.FIX42;

namespace QuantConnect.WEX.Wex
{
    public class WEXOrderRoutingSessionHandler : MessageCracker, IWEXFixSessionHandler, IFixOutboundBrokerageHandler
    {
        private readonly ISession _session;
        private readonly WEXSymbolMapper _symbolMapper;
        private readonly FixConfiguration _fixConfiguration;
        private readonly IFixBrokerageController _fixBrokerageController;

        public bool IsReady { get; set; }

        public WEXOrderRoutingSessionHandler(WEXSymbolMapper symbolMapper, ISession session, IFixBrokerageController fixBrokerageController, FixConfiguration fixConfiguration)
        {
            _symbolMapper = symbolMapper;
            _fixConfiguration = fixConfiguration;
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _fixBrokerageController = fixBrokerageController ?? throw new ArgumentNullException(nameof(fixBrokerageController));

            fixBrokerageController.Register(this);
        }

        public bool CancelOrder(Order order)
        {
            return _session.Send(new OrderCancelRequest
            {
                ClOrdID = new ClOrdID(WEXOrderId.GetNext()),
                OrigClOrdID = new OrigClOrdID(order.BrokerId[0])
            });
        }

        public bool PlaceOrder(Order order)
        {
            var side = new Side(order.Direction == OrderDirection.Buy ? Side.BUY : Side.SELL);

            var ticker = _symbolMapper.GetBrokerageSymbol(order.Symbol);
            var securityType = new QuickFix.Fields.SecurityType(_symbolMapper.GetBrokerageSecurityType(order.Symbol.SecurityType));

            var wexOrder = new NewOrderSingle
            {
                ClOrdID = new ClOrdID(WEXOrderId.GetNext()),
                HandlInst = new HandlInst(HandlInst.AUTOMATED_EXECUTION_ORDER_PUBLIC_BROKER_INTERVENTION_OK),
                //ExDestination = new ExDestination("O"),
                Symbol = new QuickFix.Fields.Symbol(ticker),
                SecurityType = securityType,
                //MaturityMonthYear = new MaturityMonthYear(),
                Side = side,
                TransactTime = new TransactTime(DateTime.UtcNow),
                OrderQty = new OrderQty(order.AbsoluteQuantity),
                TimeInForce = Utility.ConvertTimeInForce(order.TimeInForce, order.Type),
                Rule80A = new Rule80A(Rule80A.AGENCY_SINGLE_ORDER),
                Account = new Account(_fixConfiguration.Account)
            };

            if (order.Symbol.SecurityType == SecurityType.Option)
            {
                wexOrder.MaturityMonthYear = Utility.GetMaturityMonthYear(order.Symbol);
                // wexOrder.PutOrCall = 
                wexOrder.StrikePrice = new StrikePrice(decimal.Round(order.Price, Utility.LIMIT_DECIMAL_PLACE));
                // wexOrder.UnderlyingSymbol
            }

            switch (order.Type)
            {
                case OrderType.Market:
                    wexOrder.OrdType = new OrdType(OrdType.MARKET);
                    break;
                case OrderType.Limit:
                    wexOrder.OrdType = new OrdType(OrdType.LIMIT);
                    wexOrder.Price = new Price(((LimitOrder)order).LimitPrice);
                    break;
                case OrderType.StopMarket:
                    wexOrder.OrdType = new OrdType(OrdType.STOP);
                    wexOrder.StopPx = new StopPx(((StopMarketOrder)order).StopPrice);
                    break;
                case OrderType.StopLimit:
                    wexOrder.OrdType = new OrdType(OrdType.STOP_LIMIT);
                    wexOrder.Price = new Price(((StopLimitOrder)order).LimitPrice);
                    wexOrder.StopPx = new StopPx(((StopLimitOrder)order).StopPrice);
                    break;
                case OrderType.MarketOnClose:
                    wexOrder.OrdType = new OrdType(OrdType.MARKET_ON_CLOSE);
                    break;
                default:
                    Logging.Log.Error($"WEX doesn't support current orderType: {nameof(order.Type)}");
                    break;
            }

            order.BrokerId.Add(wexOrder.ClOrdID.getValue());

            return _session.Send(wexOrder);
        }

        public bool RequestOpenOrders()
        {
            throw new NotImplementedException();
        }

        public bool UpdateOrder(Order order)
        {
            //Only single-leg request

            var request = new OrderCancelReplaceRequest
            {
                //OrderID = new OrderID(?), - Not required. Unique identifier of most recent order as assigned by WEX.
                OrigClOrdID = new OrigClOrdID(order.BrokerId[0]),
                ClOrdID = new ClOrdID(WEXOrderId.GetNext()),
                //Account = new Account(?), Not required. Not supported. The account of the original order will carry through to all replacements.
                HandlInst = new HandlInst(HandlInst.AUTOMATED_EXECUTION_ORDER_PRIVATE_NO_BROKER_INTERVENTION),
                //ExecInst = new ExecInst(?), Not required. 
                //MaxFloor = new MaxFloor(?), Not required. Not supported. The MaxFloor of the original order will carry through to all replacements.
                //ExDestination = new ExDestination(?), Not required. Not supported.The ExDestination of the original order will carry through to all replacements.
                Symbol = new QuickFix.Fields.Symbol(order.Symbol.Value),
                //SymbolSfx = new SymbolSfx(?), Not required. 
                //MaturityMonthYear = new MaturityMonthYear(?), Not required. Formatted in (YYYYMM)
                //PutOrCall = new PutOrCall(?), Not required. Put=0, Call=1
                //StrikePrice = new StrikePrice(?), Not required. Prices should be positive, non-zero, and limited to 3 decimal places.
                TransactTime = new TransactTime(order.Time),
                OrderQty = new OrderQty(order.Quantity),
                //TimeInForce = new TimeInForce(?), Not required. Not supported. The TIF of the original order will carry through to all replacements.
                //EffectiveTime = new EffectiveTime(?), Not required. Not supported. The Effective Time of the original order will carry through to all replacements.
                //ExpireTime = new ExpireTime(?), Not required. Not supported.The Expire Time of   the original order will carry through to all replacements.
                //OpenClose = new OpenClose(?), REQUIRED !!! Valid values: O = opening position C = closing position
            };

            switch (order.Direction)
            {
                case OrderDirection.Buy:
                    request.Side = new Side(Side.BUY);
                    break;

                case OrderDirection.Sell:
                    request.Side = new Side(Side.SELL);
                    break;

                //case OrderDirection.SellShort:
                //    request.Side = new Side(Side.SELL_SHORT);
                //    break;

                default:
                    Logging.Log.Error($"WEXOrderRoutingSessionHandler.UpdateOrder(): Not supported order direction - {order.Type}");
                    break;
            }

            switch (order.Type)
            {
                case OrderType.Market:
                    request.OrdType = new OrdType(OrdType.MARKET);
                    break;

                case OrderType.Limit:
                    request.OrdType = new OrdType(OrdType.LIMIT);
                    request.Price = new Price(order.Price);
                    break;

                //case OrderType.Stop:
                //    request.OrdType = new OrdType(OrdType.STOP);
                //    break;

                case OrderType.StopLimit:
                    request.OrdType = new OrdType(OrdType.STOP_LIMIT);
                    request.Price = new Price(order.Price);
                    break;

                case OrderType.MarketOnClose:
                    request.OrdType = new OrdType(OrdType.MARKET_ON_CLOSE);
                    break;

                //case OrderType.LimitOnClose:
                //    request.OrdType = new OrdType(OrdType.LIMIT_ON_CLOSE);
                //    request.Price = new Price(order.Price);
                //    break;

                //case OrderType.Pegged:
                //    request.OrdType = new OrdType(OrdType.PEGGED);
                //    request.PegDifference = new PegDifference(???)
                //    break;

                default:
                    Logging.Log.Error($"WEXOrderRoutingSessionHandler.UpdateOrder(): Not supported order type - {order.Type}");
                    break;
            }

            //request.

            return _session.Send(request);
        }

        public void OnMessage(OrderCancelReject rejection, SessionID _)
        {
            var reason = rejection.CxlRejReason.DescribeInt(rejection.IsSetCxlRejReason());
            var responseTo = rejection.CxlRejResponseTo.DescribeChar(rejection.IsSetCxlRejResponseTo());
            var text = rejection.IsSetText() ? rejection.Text.getValue() : "<no-text>";
            Logging.Log.Error($"Order cancellation failed: {reason}: {text} (response to:{responseTo})");
        }

        public void OnMessage(ExecutionReport execution, SessionID _)
        {
            Logging.Log.Error($"WEXOrderRoutingSessionHandler: OnMessage(ExecutionReport _): {execution})");

            var orderId = execution.OrderID.getValue();
            var clOrdId = execution.IsSetClOrdID() ? execution.ClOrdID.getValue() : string.Empty;
            var execType = execution.ExecType.getValue();

            var orderStatus = Utility.ConvertOrderStatus(execution);

            if (!clOrdId.IsNullOrEmpty())
            {
                if (orderStatus != OrderStatus.Invalid)
                {
                    Logging.Log.Trace($"ExecutionReport: Id: {orderId}, ClOrdId: {clOrdId}, ExecType: {execType}, OrderStatus: {orderStatus}");
                }
                else
                {
                    Logging.Log.Error($"ExecutionReport: Id: {orderId}, ClOrdId: {clOrdId}, ExecType: {execType}, OrderStatus: {orderStatus}");
                }
            }

            var isStatusRequest = execution.IsSetExecTransType() && execution.ExecTransType.getValue() == ExecTransType.STATUS;

            if (!isStatusRequest)
            {
                _fixBrokerageController.Receive(execution);
            }

            if (isStatusRequest)
            {
                _fixBrokerageController.OnOpenOrdersReceived();
            }
        }
    }
}
