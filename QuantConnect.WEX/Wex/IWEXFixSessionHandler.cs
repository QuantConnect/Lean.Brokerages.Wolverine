using QuickFix;

namespace QuantConnect.WEX.Wex
{
    public interface IWEXFixSessionHandler
    {
        bool IsReady { get; set; }

        void Crack(Message message, SessionID sessionId);
    }
}
