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

using QuantConnect.Securities;
using QuantConnect.Brokerages.Wolverine.Fix.Core;
using QuantConnect.Brokerages.Wolverine.Fix.Protocol;
using QuickFix;
using QuickFix.Fields;
using QuickFix.FIX42;
using System.Collections.Concurrent;
using Message = QuickFix.Message;

namespace QuantConnect.Brokerages.Wolverine
{
    public class WolverineFixProtocolDirector : IFixProtocolDirector
    {
        private readonly ISecurityProvider _securityProvider;
        private readonly WolverineSymbolMapper _symbolMapper;
        private readonly string _account;
        private readonly IFixBrokerageController _fixBrokerageController;

        private readonly ConcurrentDictionary<SessionID, IWolverineFixSessionHandler> _sessionHandlers = new ConcurrentDictionary<SessionID, IWolverineFixSessionHandler>();

        public WolverineFixProtocolDirector(
            WolverineSymbolMapper symbolMapper,
            string account,
            IFixBrokerageController fixBrokerageController,
            ISecurityProvider securityProvider)
        {
            _account = account;
            _symbolMapper = symbolMapper;
            _securityProvider = securityProvider;
            _fixBrokerageController = fixBrokerageController;
        }

        public IMessageFactory MessageFactory { get; } = new MessageFactory();

        public bool AreSessionsReady()
        {
            return _sessionHandlers.IsEmpty ? false : _sessionHandlers.All(kvp => kvp.Value.IsReady && Session.LookupSession(kvp.Key).IsLoggedOn);
        }

        public void EnrichOutbound(Message msg)
        {
            switch (msg)
            {
                case Logon logon:
                    logon.SetField(new ResetSeqNumFlag(ResetSeqNumFlag.YES));
                    logon.SetField(new EncryptMethod(EncryptMethod.NONE));
                    break;
            }
        }

        public void Handle(Message msg, SessionID sessionId)
        {
            if (!_sessionHandlers.TryGetValue(sessionId, out var handler))
            {
                Logging.Log.Error("WolverineFixProtocolDirector.Handle(): Unknown session: " + sessionId);
                return;
            }

            try
            {
                handler.Crack(msg, sessionId);
            }
            catch (Exception e)
            {
                Logging.Log.Error(e, $"WolverineFixProtocolDirector.Handle(): [{sessionId}] Unable to process message {msg.GetType().Name}: {msg}");
            }
        }

        public void OnLogon(SessionID sessionId)
        {
            Logging.Log.Trace($"WolverineFixProtocolDirector.OnLogon(): Adding handler for SessionId: {sessionId}");

            var session = new QuickFixSession(sessionId);

            _sessionHandlers[sessionId] = new WolverineOrderRoutingSessionHandler(_symbolMapper, session, _fixBrokerageController, _account, _securityProvider)
            {
                IsReady = true
            };
        }

        public void OnLogout(SessionID sessionId)
        {
            Logging.Log.Trace($"WolverineFixProtocolDirector.OnLogout(): Removing handler for SessionId: {sessionId}");

            if (_sessionHandlers.TryRemove(sessionId, out var handler))
            {
                _fixBrokerageController.Unregister((IFixOutboundBrokerageHandler)handler);
            }
        }

        public void HandleAdminMessage(Message msg)
        {
            switch (msg)
            {
                case Heartbeat:
                    Logging.Log.Trace($"{msg.GetType().Name}: {msg}");
                    break;
            }
        }
    }
}
