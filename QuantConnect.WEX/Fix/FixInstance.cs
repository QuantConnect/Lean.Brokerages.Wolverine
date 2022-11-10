/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using QuickFix;
using QuantConnect.Util;
using QuickFix.Transport;
using QuantConnect.Securities;
using QuantConnect.WEX.Fix.Protocol;
using QuantConnect.WEX.Fix.LogFactory;

namespace QuantConnect.WEX.Fix
{
    /// <summary>
    /// The instance of a single QuickFIX/n configuration
    /// This includes session to interact between client and brokerage.
    /// </summary>
    public class FixInstance : IApplication, IDisposable
    {
        private readonly IFixProtocolDirector _protocolDirector;
        private readonly SocketInitiator _initiator;
        private SecurityExchangeHours _securityExchangeHours;

        private bool _disposed;
        private volatile bool _connected;
        private CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// Event invoke to show problem with FIX protocol
        /// </summary>
        public event EventHandler<FixError> Error;

        public FixInstance(IFixProtocolDirector protocolDirector, FixConfiguration fixConfiguration, bool logFixMessages)
        {
            _protocolDirector = protocolDirector ?? throw new ArgumentNullException(nameof(protocolDirector));

            var settings = fixConfiguration.GetDefaultSessionSettings();

            var storeFactory = new FileStoreFactory(settings);
            var logFactory = new QuickFixLogFactory(logFixMessages);
            _initiator = new SocketInitiator(this, storeFactory, settings, logFactory, protocolDirector.MessageFactory);

            _securityExchangeHours = MarketHoursDatabase.FromDataFolder().GetExchangeHours(Market.USA, null, SecurityType.Equity);
        }

        public bool IsConnected() => _connected && !_disposed;

        public void Initialize()
        {
            _connected = TryConnect();
            _cancellationTokenSource = new CancellationTokenSource();

            Task.Factory.StartNew(() =>
            {
                Logging.Log.Trace($"FixInstance(): starting fix connection monitor...");

                var retry = 0;
                var timeoutLoop = TimeSpan.FromMinutes(1);
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    if (_cancellationTokenSource.Token.WaitHandle.WaitOne(timeoutLoop))
                    {
                        // exit time
                        break;
                    }

                    if (!TryConnect())
                    {
                        Logging.Log.Error($"FixInstance(): connection failed");

                        if (++retry >= 5)
                        {
                            // after retrying to connect for X times & exchange is open we should die
                            Error?.Invoke(this, new FixError { Message = "Fix connection failed" });
                        }
                    }
                    else
                    {
                        retry = 0;
                    }
                }
                Logging.Log.Trace($"FixInstance(): ending connection monitor");
            });
        }

        /// <summary>
        /// Every inbound admin level message will pass through this method, such as heartbeats, logons, and logouts.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="sessionID"></param>
        public void FromAdmin(Message message, SessionID sessionID) 
        {
            _protocolDirector.HandleAdminMessage(message);
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
        public void OnCreate(SessionID sessionID) { }

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
            _connected = false;
            // stop fix connection monitor
            _cancellationTokenSource.Cancel();
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
            _initiator.DisposeSafely();
            _cancellationTokenSource.DisposeSafely();
        }

        private bool TryConnect()
        {
            try
            {
                if (_initiator.IsStopped && IsExchangeOpen(extendedMarketHours: true))
                {
                    // while the exchange is open and we are not connected, let's try to connect
                    StartConnection();

                    // TODO: there's a potential race condition here? Will the 'Start()' call above resolve completely or we need to wait for a login reply message to come in
                    return IsConnectedAllSession() && _protocolDirector.AreSessionsReady();
                }

                // we are already connected or exchange is closed
                return true;
            }
            catch (Exception ex)
            {
                Logging.Log.Error(ex);
            }

            // something failed
            return false;
        }

        private void StartConnection()
        {
            _initiator.Start();

            var connectionCheckerCounter = 0;

            while(!IsConnectedAllSession() || !_protocolDirector.AreSessionsReady())
            {
                connectionCheckerCounter++;

                if (connectionCheckerCounter > 10)
                    break;

                Thread.Sleep(1000);
            }
        }
        private bool IsConnectedAllSession()
        {
            return !_initiator.IsStopped &&
                   _initiator.GetSessionIDs()
                        .Select(Session.LookupSession)
                        .All(session => session != null && session.IsLoggedOn);
        }
    }
}
