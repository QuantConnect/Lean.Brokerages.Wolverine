using QuickFix;

namespace QuantConnect.WEX.Fix.Core
{
    public class QuickFixSession : ISession
    {
        private readonly Session _session;

        public QuickFixSession(SessionID sessionId)
        {
            if (sessionId == null)
            {
                throw new ArgumentNullException(nameof(sessionId));
            }

            _session = Session.LookupSession(sessionId) ?? throw new SessionNotFound(sessionId);
        }

        public bool Send(Message message)
        {
            return _session.Send(message);
        }
    }
}
