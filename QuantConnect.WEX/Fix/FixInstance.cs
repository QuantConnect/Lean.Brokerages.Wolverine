using QuickFix;
using QuickFix.Transport;
using QuantConnect.WEX.Fix.Protocol;
using QuantConnect.WEX.Fix.LogFactory;
using QuantConnect.Securities;

namespace QuantConnect.WEX.Fix
{
    public class FixInstance : IApplication, IDisposable
    {
        private readonly IFixProtocolDirector _protocolDirector;
        private readonly FixConfiguration _fixConfiguration;
        private readonly SocketInitiator _initiator;
        private SecurityExchangeHours _securityExchangeHours;

        private bool _disposed;

        public FixInstance(IFixProtocolDirector protocolDirector, FixConfiguration fixConfiguration, bool logFixMessages)
        {
            _protocolDirector = protocolDirector ?? throw new ArgumentNullException(nameof(protocolDirector));
            _fixConfiguration = fixConfiguration;

            var settings = fixConfiguration.GetDefaultSessionSettings();

            var storeFactory = new FileStoreFactory(settings);
            var logFactory = new QuickFixLogFactory(logFixMessages);
            _initiator = new SocketInitiator(this, storeFactory, settings, logFactory);

            _securityExchangeHours = MarketHoursDatabase.FromDataFolder().GetExchangeHours(Market.USA, null, SecurityType.Equity);
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
            if (!IsExchangeOpen(extendedMarketHours: false))
            {
                Logging.Log.Error($"WEX.Initialise(ExchangeOpen: false)");
                return;
            }

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

        /// <summary>
        /// Every inbound admin level message will pass through this method, such as heartbeats, logons, and logouts.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="sessionID"></param>
        public void FromAdmin(Message message, SessionID sessionID) 
        {
            Logging.Log.Trace($"admin level message: {message.GetType().Name}: {message}");

            //TODO: Implement Re-Logon when we have caught not correct MsgSeqNum
            //switch(message)
            //{
            //    case QuickFix.FIX42.Logout logout:
            //        var text = message.GetString(QuickFix.Fields.Text.TAG);
            //        break;
            //}
        }

        /// <summary>
        /// Every inbound application level message will pass through this method, such as orders, executions, security definitions, and market data
        /// </summary>
        /// <param name="message"></param>
        /// <param name="sessionID"></param>
        public void FromApp(Message message, SessionID sessionID)
        {
            try
            {
                _protocolDirector.Handle(message, sessionID);
            }
            catch (UnsupportedMessageType e)
            {
                Logging.Log.Error(e, $"[{sessionID}] Unknown message: {message.GetType().Name}: {message}");
            }
        }

        /// <summary>
        /// This method is called whenever a new session is created.
        /// </summary>
        /// <param name="sessionID"></param>
        public void OnCreate(SessionID sessionID) 
        {
            Logging.Log.Trace($"admin level message: {sessionID.GetType().Name}: {sessionID}");
        }

        /// <summary>
        /// Notifies when a successful logon has completed.
        /// </summary>
        /// <param name="sessionID"></param>
        public void OnLogon(SessionID sessionID)
        {
            _protocolDirector.OnLogon(sessionID);
        }

        /// <summary>
        /// Notifies when a session is offline - either from an exchange of logout messages or network connectivity loss.
        /// </summary>
        /// <param name="sessionID"></param>
        public void OnLogout(SessionID sessionID)
        {
            _protocolDirector.OnLogout(sessionID);
        }

        /// <summary>
        /// All outbound admin level messages pass through this callback.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="sessionID"></param>
        public void ToAdmin(Message message, SessionID sessionID)
        {
            _protocolDirector.EnrichOutbound(message);
        }

        /// <summary>
        /// All outbound application level messages pass through this callback before they are sent. 
        /// If a tag needs to be added to every outgoing message, this is a good place to do that.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="sessionID"></param>
        public void ToApp(Message message, SessionID sessionID) { }

        private bool IsExchangeOpen(bool extendedMarketHours)
        {
            var localTime = DateTime.UtcNow.ConvertFromUtc(_securityExchangeHours.TimeZone);
            return _securityExchangeHours.IsOpen(localTime, extendedMarketHours);
        }

        public void Terminate()
        {
            if (!_initiator.IsStopped)
            {
                _initiator.Stop();
            }
        }

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
