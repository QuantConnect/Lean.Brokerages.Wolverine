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

using QuickFix;
using QuickFix.FIX42;
using QuickFix.Fields;
using QuantConnect.Orders;
using System.Globalization;
using QuantConnect.Securities;
using QuantConnect.Securities.Equity;
using QuantConnect.Brokerages.Fix.Connection;
using QuantConnect.Brokerages.Fix.Core.Interfaces;

namespace QuantConnect.Brokerages.Wolverine
{
    public class WolverineOrderRoutingSessionHandler : MessageCracker, IFixOrdersController
    {
        private readonly SymbolPropertiesDatabase _symbolPropertiesDatabase = SymbolPropertiesDatabase.FromDataFolder();
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
        private readonly ISecurityProvider _securityProvider;
        private readonly WolverineSymbolMapper _symbolMapper;

        public IFixConnection Session { get; set; }

        public WolverineOrderRoutingSessionHandler(WolverineSymbolMapper symbolMapper, string account, ISecurityProvider securityProvider)
        {
            _symbolMapper = symbolMapper;
            _account = new Account(account);
            _securityProvider = securityProvider;
        }

        public bool CancelOrder(Order order)
        {
            var orderToCancel = new OrderCancelRequest
            {
                ClOrdID = new ClOrdID(WolverineOrderId.GetNext()),
                OrigClOrdID = new OrigClOrdID(order.BrokerId[0])
            };
            return Session.Send(orderToCancel);
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

            if (order.Symbol.SecurityType.IsOption())
            {
                wexOrder.StrikePrice = new StrikePrice(decimal.Round(order.Price, Utility.LIMIT_DECIMAL_PLACE));

                var expirationDate = order.Symbol.ID.Date;
                wexOrder.SetField(new MaturityMonthYear(expirationDate.ToString("yyyyMM", CultureInfo.InvariantCulture)));
                wexOrder.SetField(new MaturityDay(expirationDate.Day.ToString(CultureInfo.InvariantCulture)));
                wexOrder.SetField(new ContractMultiplier(GetSymbolProperties(order.Symbol).ContractMultiplier));
                wexOrder.SetField(new PutOrCall(order.Symbol.ID.OptionRight == OptionRight.Call ? PutOrCall.CALL : PutOrCall.PUT));
            }
            else if (order.Symbol.SecurityType == SecurityType.Future)
            {
                var expirationDate = order.Symbol.ID.Date;
                wexOrder.SetField(new MaturityMonthYear(expirationDate.ToString("yyyyMM", CultureInfo.InvariantCulture)));
                wexOrder.SetField(new MaturityDay(expirationDate.Day.ToString(CultureInfo.InvariantCulture)));
                wexOrder.SetField(new ContractMultiplier(GetSymbolProperties(order.Symbol).ContractMultiplier));
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

            return Session.Send(wexOrder);
        }

        public bool UpdateOrder(Order order)
        {
            throw new NotImplementedException();
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

        private SymbolProperties GetSymbolProperties(Symbol symbol)
        {
            return _symbolPropertiesDatabase.GetSymbolProperties(symbol.ID.Market, symbol, symbol.SecurityType, Currencies.USD);
        }
    }
}
