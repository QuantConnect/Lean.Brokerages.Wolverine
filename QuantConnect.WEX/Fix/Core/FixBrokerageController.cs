using QuantConnect.Orders;
using QuantConnect.WEX.Fix.Protocol;
using QuickFix.FIX42;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantConnect.WEX.Fix.Core
{
    public class FixBrokerageController : IFixBrokerageController
    {
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
            throw new NotImplementedException();
        }

        public void OnOpenOrdersReceived()
        {
            throw new NotImplementedException();
        }

        public bool PlaceOrder(Order order)
        {
            return _handler.PlaceOrder(order);
        }

        public void Receive(ExecutionReport orderEvent)
        {
            throw new NotImplementedException();
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
    }
}
