using QuantConnect.WEX.Fix;
using QuantConnect.WEX.Fix.Core;
using QuantConnect.WEX.Fix.Protocol;
using QuickFix;
using QuickFix.Fields;
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
            return _sessionHandlers.Values.All(handler => handler.IsReady);
        }

        public void EnrichOutbound(Message msg)
        {
            switch (msg)
            {
                case Logon logon:
                    logon.SetField(new ResetSeqNumFlag(ResetSeqNumFlag.YES));
                    logon.SetField(new MsgSeqNum(1));
                    logon.SetField(new EncryptMethod(EncryptMethod.NONE));
                    logon.SetField(new OnBehalfOfCompID(_fixConfiguration.OnBehalfOfCompID));
                    break;
            }
        }

        public void Handle(Message msg, SessionID sessionId)
        {
            if (!_sessionHandlers.TryGetValue(sessionId, out var handler))
            {
                Logging.Log.Error("Unknown session: " + sessionId);
                return;
            }

            try
            {
                handler.Crack(msg, sessionId);
            }
            catch (Exception e)
            {
                Logging.Log.Error(e, $"[{sessionId}] Unable to process message {msg.GetType().Name}: {msg}");
            }
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
            Logging.Log.Trace($"OnLogout(): Removing handler for SessionId: {sessionId}");

            if (_sessionHandlers.TryRemove(sessionId, out var handler))
            {
                if (sessionId.SenderCompID == _fixConfiguration.SenderCompId && sessionId.TargetCompID == _fixConfiguration.TargetCompId)
                {
                    _fixMarketDataController.Unregister((IFixOutboundMarketDataHandler)handler);
                }
            }
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
