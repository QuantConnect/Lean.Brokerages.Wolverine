using QuantConnect.Data.Market;
using QuantConnect.WEX.Fix.Protocol;

namespace QuantConnect.WEX.Fix.Core
{
    public interface IFixMarketDataController
    {
        event EventHandler<Tick> NewTick;

        void Register(IFixOutboundMarketDataHandler handler);

        void Unregister(IFixOutboundMarketDataHandler handler);

        void Subscribe(Symbol symbol);

        void Unsubscribe(Symbol symbol);

        void Receive(Tick tick);
    }
}
