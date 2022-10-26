using QuickFix;
using System.Collections.Concurrent;

namespace QuantConnect.WEX.Fix.LogFactory
{
    public class QuickFixLogFactory : ILogFactory
    {
        private static readonly ConcurrentDictionary<SessionID, ILog> Loggers = new ConcurrentDictionary<SessionID, ILog>();
        private readonly bool _logFixMessages;

        public QuickFixLogFactory(bool logFixMessages)
        {
            _logFixMessages = logFixMessages;
        }

        public ILog Create(SessionID sessionId)
        {
            return Loggers.GetOrAdd(sessionId, s => new QuickFixLogger(s, _logFixMessages));
        }
    }
}
