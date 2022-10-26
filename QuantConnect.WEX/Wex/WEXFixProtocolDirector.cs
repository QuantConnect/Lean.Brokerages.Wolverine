using QuantConnect.WEX.Fix;
using QuantConnect.WEX.Fix.Core;
using QuantConnect.WEX.Fix.Protocol;
using QuickFix;
using QuickFix.FIX42;
using System.Collections.Concurrent;
using Message = QuickFix.Message;

namespace QuantConnect.WEX.Wex
{
    public class WEXFixProtocolDirector : IFixProtocolDirector
    {
        private readonly FixConfiguration _fixConfiguration;
        private readonly IFixMarketDataController _fixMarketDataController;

        private readonly ConcurrentDictionary<SessionID, IWEXFixSessionHandler> _sessionHandlers = new ConcurrentDictionary<SessionID, IWEXFixSessionHandler>();


        public WEXFixProtocolDirector(FixConfiguration fixConfiguration, IFixMarketDataController fixMarketDataController)
        {
            _fixConfiguration = fixConfiguration;
            _fixMarketDataController = fixMarketDataController;
        }

        public IMessageFactory MessageFactory { get; } = new MessageFactory();

        public bool AreSessionsReady()
        {
            throw new NotImplementedException();
        }

        public void EnrichOutbound(Message msg)
        {
            throw new NotImplementedException();
        }

        public void Handle(Message msg, SessionID sessionId)
        {
            throw new NotImplementedException();
        }

        public void OnLogon(SessionID sessionId)
        {
            Logging.Log.Trace($"OnLogon(): Adding handler for SessionId: {sessionId}");

            var session = new QuickFixSession(sessionId);
            var handler = CreateSessionHandler(sessionId.SenderCompID, sessionId.TargetCompID, session);
            _sessionHandlers[sessionId] = handler;
        }

        public void OnLogout(SessionID sessionId)
        {
            throw new NotImplementedException();
        }

        private IWEXFixSessionHandler CreateSessionHandler(string senderCompId, string targetCompId, ISession session)
        {
            if (senderCompId == _fixConfiguration.SenderCompId && targetCompId == _fixConfiguration.TargetCompId)
            {
                return new WEXMarketDataSessionHandler(session, _fixMarketDataController);
            }

            throw new Exception($"Unknown session senderCompId: '{senderCompId}'");
        }
    }
}
