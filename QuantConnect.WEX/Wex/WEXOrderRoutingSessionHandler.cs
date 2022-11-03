using QuantConnect.Orders;
using QuantConnect.WEX.Fix.Core;
using QuantConnect.WEX.Fix.Protocol;
using QuickFix.Fields;
using QuickFix.FIX42;

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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }
    }
}
