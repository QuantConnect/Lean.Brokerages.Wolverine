using QuantConnect.Orders;
using QuantConnect.WEX.Fix.Protocol;
using QuantConnect.WEX.Fix.Utils;
using QuickFix.Fields;
using QuickFix.FIX42;
using System.Collections.Concurrent;

namespace QuantConnect.WEX.Fix.Core
{
    public class FixBrokerageController : IFixBrokerageController
    {
        private readonly ConcurrentDictionary<string, ExecutionReport> _orders = new ConcurrentDictionary<string, ExecutionReport>();

        private readonly WEXSymbolMapper _symbolMapper;
        private IFixOutboundBrokerageHandler _handler;

        public event EventHandler<ExecutionReport> ExecutionReport;

        public FixBrokerageController(WEXSymbolMapper symbolMapper)
        {
            _symbolMapper = symbolMapper;
        }

        public bool CancelOrder(Order order)
        {
            return _handler.CancelOrder(order);
        }

        public List<Order> GetOpenOrders()
        {
            return _orders.Values
                .Select(ConvertOrder)
                .Where(x => x.Status.IsOpen())
                .ToList();
        }

        public void OnOpenOrdersReceived()
        {
            throw new NotImplementedException();
        }

        public bool PlaceOrder(Order order)
        {
            return _handler.PlaceOrder(order);
        }

        public void Receive(ExecutionReport execution)
        {
            if (execution == null)
            {
                throw new ArgumentNullException(nameof(execution));
            }

            var orderId = execution.ClOrdID.getValue();
            var orderStatus = execution.OrdStatus.getValue();
            if (orderStatus != OrdStatus.REJECTED)
            {
                _orders[orderId] = execution;
            }
            else
            {
                _orders.TryRemove(orderId, out _);
            }

            ExecutionReport?.Invoke(this, execution);
        }

        public void Register(IFixOutboundBrokerageHandler handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (_handler != null)
            {
                throw new Exception(
                    $"A handler has already been registered: {_handler.GetType().FullName}#{_handler.GetHashCode()}, received: {handler.GetType().FullName}#{handler.GetHashCode()}");
            }

            _handler = handler;
        }

        public bool RequestOpenOrders()
        {
            throw new NotImplementedException();
        }

        public void Unregister(IFixOutboundBrokerageHandler handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (_handler == null || handler != _handler)
            {
                throw new Exception(
                    $"The handler has not been registered: {handler.GetType().FullName}#{handler.GetHashCode()}");
            }

            _handler = null;
        }

        public bool UpdateOrder(Order order)
        {
            return _handler.UpdateOrder(order);
        }

        private Order ConvertOrder(ExecutionReport er)
        {
            if (er == null)
            {
                throw new ArgumentNullException(nameof(er));
            }

            var ticker = er.Symbol.getValue();
            var securityType = _symbolMapper.GetLeanSecurityType(er.SecurityType.getValue());

            //var market = _symbolMapper.GetLeanMarket(securityType, er.SecurityExchange.getValue(), ticker);

            Symbol symbol = Symbol.Create(ticker, securityType, ticker);

            var orderQuantity = er.OrderQty.getValue();
            var orderSide = er.Side.getValue();
            if (orderSide == Side.SELL)
            {
                orderQuantity = -orderQuantity;
            }

            var time = er.TransactTime.getValue();
            var orderType = Utility.ConvertOrderType(er.OrdType.getValue());
            var timeInForce = Utility.ConvertTimeInForce(er.TimeInForce.getValue());

            Order order;
            switch (orderType)
            {
                case OrderType.Market:
                    order = new MarketOrder();
                    break;

                case OrderType.Limit:
                    {
                        var limitPrice = er.Price.getValue();
                        order = new LimitOrder(symbol, orderQuantity, limitPrice, time);
                    }
                    break;

                case OrderType.StopMarket:
                    {
                        var stopPrice = er.StopPx.getValue();
                        order = new LimitOrder(symbol, orderQuantity, stopPrice, time);
                    }
                    break;

                case OrderType.StopLimit:
                    {
                        var limitPrice = er.Price.getValue();
                        var stopPrice = er.StopPx.getValue();
                        order = new StopLimitOrder(symbol, orderQuantity, stopPrice, limitPrice, time);
                    }
                    break;

                default:
                    throw new NotSupportedException($"Unsupported order type: {orderType}");
            }

            order.Properties.TimeInForce = timeInForce;

            order.BrokerId.Add(er.ClOrdID.getValue());

            return order;
        }
    }
}
