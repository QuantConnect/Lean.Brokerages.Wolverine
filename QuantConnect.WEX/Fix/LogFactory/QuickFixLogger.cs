using QuickFix;

namespace QuantConnect.WEX.Fix.LogFactory
{
    public class QuickFixLogger : ILog
    {
        private readonly bool _fixLoggingEnabled;

        public QuickFixLogger(SessionID sessionId, bool logFixMessages)
        {
            if (sessionId == null)
            {
                throw new ArgumentNullException(nameof(sessionId));
            }

            _fixLoggingEnabled = logFixMessages;
        }

        public void Clear() { }

        public void OnIncoming(string msg)
        {
            if (_fixLoggingEnabled && ShouldLogMessage(msg))
            {
                Logging.Log.Trace($"[incoming] {msg.Replace('\x1', '|')}", true);
            }
        }

        public void OnOutgoing(string msg)
        {
            if (_fixLoggingEnabled && ShouldLogMessage(msg))
            {
                Logging.Log.Trace($"[outgoing] {msg.Replace('\x1', '|')}", true);
            }
        }

        public void OnEvent(string s)
        {
            if (_fixLoggingEnabled)
            {
                Logging.Log.Trace($"[   event] {s.Replace('\x1', '|')}", true);
            }
        }

        public void Dispose()
        {
        }

        private static bool ShouldLogMessage(string msg)
        {
            if (msg.Contains($"{'\x1'}35=0{'\x1'}"))
            {
                // exclude heartbeats
                return false;
            }

            if (msg.Contains($"{'\x1'}35=W{'\x1'}"))
            {
                // exclude market data snapshot messages
                return false;
            }

            if (msg.Contains($"{'\x1'}35=X{'\x1'}"))
            {
                // exclude market data incremental refresh messages
                return false;
            }

            return true;
        }
    }
}
