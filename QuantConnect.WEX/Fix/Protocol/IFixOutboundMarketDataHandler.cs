namespace QuantConnect.WEX.Fix.Protocol
{
    public interface IFixOutboundMarketDataHandler
    {
        bool SubscribeToSymbol(Symbol symbol);
        bool UnsubscribeFromSymbol(Symbol symbol);
    }
}
