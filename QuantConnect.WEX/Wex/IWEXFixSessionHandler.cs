using QuickFix;

namespace QuantConnect.WEX.Wex
{
    public interface IWEXFixSessionHandler
    {
        bool IsReady { get; }

        void Crack(Message message, SessionID sessionId);
    }
}
