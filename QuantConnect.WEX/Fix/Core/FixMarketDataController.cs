using QuantConnect.Data.Market;
using QuantConnect.Logging;
using QuantConnect.WEX.Fix.Protocol;

namespace QuantConnect.WEX.Fix.Core
{
    public class FixMarketDataController : IFixMarketDataController
    {
        private IFixOutboundMarketDataHandler _handler;

        public event EventHandler<Tick> NewTick;

        public void Register(IFixOutboundMarketDataHandler handler)
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

        public void Unregister(IFixOutboundMarketDataHandler handler)
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

        public void Subscribe(Symbol symbol)
        {
            if (_handler == null)
            {
                Log.Error("No market data handler has been registered.");
                return;
            }

            _handler.SubscribeToSymbol(symbol);
        }

        public void Unsubscribe(Symbol symbol)
        {
            if (_handler == null)
            {
                Log.Error("No market data handler has been registered.");
                return;
            }

            _handler.UnsubscribeFromSymbol(symbol);
        }

        public void Receive(Tick tick)
        {
            NewTick?.Invoke(this, tick);
        }
    }
}
