/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using QuantConnect.Orders;
using QuantConnect.Securities;
using QuantConnect.Securities.Equity;
using QuantConnect.Util;
using QuantConnect.Brokerages.Wolverine.Fix.Core;
using QuantConnect.Brokerages.Wolverine.Fix.Protocol;
using QuantConnect.Brokerages.Wolverine.Fix.Utils;
using QuickFix;
using QuickFix.Fields;
using QuickFix.FIX42;

namespace QuantConnect.Brokerages.Wolverine
{
    public class WolverineOrderRoutingSessionHandler : MessageCracker, IWolverineFixSessionHandler, IFixOutboundBrokerageHandler
    {
        private readonly Dictionary<string, string> _exchangeMapping = new() {
            { Exchange.AMEX.Name, "AMEX" },
            { Exchange.ARCA.Name, "ARCA" },
            { Exchange.BATS.Name, "BATS" },
            { Exchange.BATS_Y.Name, "BATSYX" },
            { Exchange.EDGA.Name, "EDGA" },
            { Exchange.EDGX.Name, "EDGX" },
            { Exchange.NASDAQ.Name, "NASDAQ" },
            { Exchange.NASDAQ_BX.Name, "NASDAQBX" },
            { Exchange.NYSE.Name, "NYSE" },
            { Exchange.NASDAQ_PSX.Name, "PHLX" },
            { Exchange.SMART, "SMART" },
            { Exchange.IEX, "IEX" },
            { Exchange.OTCX, "OTCX" }
        };

        private readonly Account _account;
        private readonly ISession _session;
        private readonly ISecurityProvider _securityProvider;
        private readonly WolverineSymbolMapper _symbolMapper;
        private readonly IFixBrokerageController _fixBrokerageController;

        public bool IsReady { get; set; }

        public WolverineOrderRoutingSessionHandler(WolverineSymbolMapper symbolMapper, ISession session, IFixBrokerageController fixBrokerageController, string account, ISecurityProvider securityProvider)
        {
            _symbolMapper = symbolMapper;
            _account = new Account(account);
            _securityProvider = securityProvider;
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _fixBrokerageController = fixBrokerageController ?? throw new ArgumentNullException(nameof(fixBrokerageController));

            fixBrokerageController.Register(this);
        }

        public bool CancelOrder(Order order)
        {
            var orderToCancel = new OrderCancelRequest
            {
                ClOrdID = new ClOrdID(WolverineOrderId.GetNext()),
                OrigClOrdID = new OrigClOrdID(order.BrokerId[0])
            };
            return _session.Send(orderToCancel);
        }

        /// <summary>
        /// Places a new order by FIX standard
        /// </summary>
        /// <param name="order"></param>
        /// <returns></returns>
        public bool PlaceOrder(Order order)
        {
            var side = new Side(order.Direction == OrderDirection.Buy ? Side.BUY : Side.SELL);

            var ticker = _symbolMapper.GetBrokerageSymbol(order.Symbol);
            var securityType = new QuickFix.Fields.SecurityType(_symbolMapper.GetBrokerageSecurityType(order.Symbol.SecurityType));

            var wexOrder = new NewOrderSingle
            {
                ClOrdID = new ClOrdID(WolverineOrderId.GetNext()),
                HandlInst = new HandlInst(HandlInst.AUTOMATED_EXECUTION_ORDER_PUBLIC_BROKER_INTERVENTION_OK),
                Symbol = new QuickFix.Fields.Symbol(ticker),
                SecurityType = securityType,
                Side = side,
                TransactTime = new TransactTime(DateTime.UtcNow),
                OrderQty = new OrderQty(order.AbsoluteQuantity),
                TimeInForce = Utility.ConvertTimeInForce(order.TimeInForce, order.Type),
                Rule80A = new Rule80A(Rule80A.AGENCY_SINGLE_ORDER),
                Account = _account,
                ExDestination = new ExDestination(GetOrderExchange(order))
            };

            if (order.Symbol.SecurityType == SecurityType.Option)
            {
                wexOrder.MaturityMonthYear = Utility.GetMaturityMonthYear(order.Symbol);
                wexOrder.StrikePrice = new StrikePrice(decimal.Round(order.Price, Utility.LIMIT_DECIMAL_PLACE));
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
                    Logging.Log.Error($"WolverineOrderRoutingSessionHandler.PlaceOrder(): doesn't support current orderType: {nameof(order.Type)}");
                    return false;
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// Call this method when WEX is rejected order by error 
        /// </summary>
        /// <param name="rejection"></param>
        /// <param name="_"></param>
        public void OnMessage(OrderCancelReject rejection, SessionID _)
        {
            _fixBrokerageController.Receive(rejection);
        }

        /// <summary>
        /// Call this method when WEX is executed report about order
        /// </summary>
        /// <param name="execution"></param>
        /// <param name="_"></param>
        public void OnMessage(ExecutionReport execution, SessionID _)
        {
            var orderId = execution.OrderID.getValue();
            var clOrdId = execution.IsSetClOrdID() ? execution.ClOrdID.getValue() : string.Empty;
            var execType = execution.ExecType.getValue();

            var orderStatus = Utility.ConvertOrderStatus(execution);

            if (!clOrdId.IsNullOrEmpty())
            {
                if (orderStatus != OrderStatus.Invalid)
                {
                    Logging.Log.Trace($"WolverineOrderRoutingSessionHandler.OnMessage(): ExecutionReport: Id: {orderId}, ClOrdId: {clOrdId}, ExecType: {execType}, OrderStatus: {orderStatus}");
                }
                else
                {
                    Logging.Log.Error($"WolverineOrderRoutingSessionHandler.OnMessage(): ExecutionReport: Id: {orderId}, ClOrdId: {clOrdId}, ExecType: {execType}, OrderStatus: {orderStatus}");
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
        private string GetOrderExchange(Order order)
        {
            var exchangeDestination = string.Empty;
            var orderProperties = order.Properties as OrderProperties;
            if (orderProperties != null && orderProperties.Exchange != null)
            {
                exchangeDestination = orderProperties.Exchange.ToString();
            }
            if (string.IsNullOrEmpty(exchangeDestination) && order.Symbol.SecurityType == SecurityType.Equity)
            {
                var equity = _securityProvider.GetSecurity(order.Symbol) as Equity;
                // potentially need to map this into Atreyu expected destination exchange name
                exchangeDestination = equity?.PrimaryExchange.ToString();
            }

            if (!_exchangeMapping.TryGetValue(exchangeDestination.ToUpper(), out var wolverineExchange))
            {
                wolverineExchange = "SMART";
            }

            var exchangePostFix = string.Empty;
            var wolverineOrderProperties = order.Properties as WolverineOrderProperties;
            if (wolverineOrderProperties != null && !string.IsNullOrEmpty(wolverineOrderProperties.ExchangePostFix))
            {
                exchangePostFix = wolverineOrderProperties.ExchangePostFix;
            }
            return wolverineExchange + exchangePostFix;
        }
    }
}
