using QuickFix;
using QuickFix.Transport;
using QuantConnect.WEX.Fix.Protocol;
using QuantConnect.WEX.Fix.LogFactory;

namespace QuantConnect.WEX.Fix
{
    public class FixInstance : IApplication, IDisposable
    {
        private readonly IFixProtocolDirector _protocolDirector;
        private readonly FixConfiguration _fixConfiguration;
        private readonly SocketInitiator _initiator;

        private bool _disposed;

        public FixInstance(IFixProtocolDirector protocolDirector, FixConfiguration fixConfiguration, bool logFixMessages)
        {
            _protocolDirector = protocolDirector ?? throw new ArgumentNullException(nameof(protocolDirector));
            _fixConfiguration = fixConfiguration;

            var settings = fixConfiguration.GetDefaultSessionSettings();

            var storeFactory = new FileStoreFactory(settings);
            var logFactory = new QuickFixLogFactory(logFixMessages);
            _initiator = new SocketInitiator(this, storeFactory, settings, logFactory);
        }

        public bool IsConnected()
        {
            return !_initiator.IsStopped &&
                   _initiator.GetSessionIDs()
                        .Select(Session.LookupSession)
                        .All(session => session != null && session.IsLoggedOn);
        }

        public void Initialise()
        {
            if (_initiator.IsStopped)
            {
                _initiator.Start();

                var start = DateTime.UtcNow;
                while (!IsConnected() || !_protocolDirector.AreSessionsReady())
                {
                    if (DateTime.UtcNow > start.AddSeconds(60))
                    {
                        throw new TimeoutException("Timeout initializing FIX sessions.");
                    }

                    Thread.Sleep(1000);
                }
            }
        }

        public void FromAdmin(Message message, SessionID sessionID)
        {
            throw new NotImplementedException();
        }

        public void FromApp(Message message, SessionID sessionID)
        {
            throw new NotImplementedException();
        }

        public void OnCreate(SessionID sessionID) { }

        public void OnLogon(SessionID sessionID)
        {
            throw new NotImplementedException();
        }

        public void OnLogout(SessionID sessionID)
        {
            throw new NotImplementedException();
        }

        public void ToAdmin(Message message, SessionID sessionID)
        {
            throw new NotImplementedException();
        }

        public void ToApp(Message message, SessionID sessionID) { }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _initiator.Dispose();
        }
    }
}
