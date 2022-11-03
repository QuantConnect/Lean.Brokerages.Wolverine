using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.WEX.Fix.Core;
using QuantConnect.WEX.Fix.Protocol;
using QuickFix;
using QuickFix.Fields;
using QuickFix.FIX42;
using System.Net.Http.Headers;
using System.Windows.Markup;

namespace QuantConnect.WEX.Wex
{
    public class WEXOrderRoutingSessionHandler : WEXFixSessionHandlerBase, IFixOutboundBrokerageHandler
    {
        private int _initialCount;

        private readonly ISession _session;
        private readonly WEXSymbolMapper _symbolMapper;
        private readonly IFixBrokerageController _fixBrokerageController;

        public WEXOrderRoutingSessionHandler(WEXSymbolMapper symbolMapper, ISession session, IFixBrokerageController fixBrokerageController)
        {
            _symbolMapper = symbolMapper;
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _fixBrokerageController = fixBrokerageController ?? throw new ArgumentNullException(nameof(fixBrokerageController));

            fixBrokerageController.Register(this);
        }

        protected override void OnRecoveryCompleted()
        {
            IsReady = true;
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
            throw new NotImplementedException();
        }

        public bool RequestOpenOrders()
        {
            throw new NotImplementedException();
        }

        public bool UpdateOrder(Order order)
        {
            var request = new OrderCancelReplaceRequest
            {
                ClOrdID = new ClOrdID(WEXOrderId.GetNext()),
                OrigClOrdID = new OrigClOrdID(order.BrokerId[0]),
                HandlInst = new HandlInst(HandlInst.AUTOMATED_EXECUTION_ORDER_PRIVATE_NO_BROKER_INTERVENTION),
                Symbol = new QuickFix.Fields.Symbol(order.Symbol.Value),     
                TransactTime = new TransactTime(order.Time),
                OrderQty = new OrderQty(order.Quantity)
            };

            switch (order.Direction)
            {
                case OrderDirection.Buy:
                    request.Side = new Side(Side.BUY);
                    break;

                case OrderDirection.Sell:
                    request.Side = new Side(Side.SELL);
                    break;

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

                case OrderType.StopLimit:
                    request.OrdType = new OrdType(OrdType.STOP_LIMIT);
                    request.Price = new Price(order.Price);
                    break;

                case OrderType.MarketOnClose:
                    request.OrdType = new OrdType(OrdType.MARKET_ON_CLOSE);
                    break;

                default:
                    Logging.Log.Error($"WEXOrderRoutingSessionHandler.UpdateOrder(): Not supported order type - {order.Type}");
                    break;
            }

            return _session.Send(request);
        }
    }
}
