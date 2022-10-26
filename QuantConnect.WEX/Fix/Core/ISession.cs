using QuickFix;

namespace QuantConnect.WEX.Fix.Core
{
    public interface ISession
    {
        bool Send(Message message);
    }
}
