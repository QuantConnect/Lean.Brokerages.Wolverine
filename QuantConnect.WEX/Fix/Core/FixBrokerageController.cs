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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        public void Receive(ExecutionReport orderEvent)
        {
            throw new NotImplementedException();
        }

        public void Register(IFixOutboundBrokerageHandler handler)
        {
            throw new NotImplementedException();
        }

        public bool RequestOpenOrders()
        {
            throw new NotImplementedException();
        }

        public void Unregister(IFixOutboundBrokerageHandler handler)
        {
            throw new NotImplementedException();
        }

        public bool UpdateOrder(Order order)
        {
            throw new NotImplementedException();
        }
    }
}
