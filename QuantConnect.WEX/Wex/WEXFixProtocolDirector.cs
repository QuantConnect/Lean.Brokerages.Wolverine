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
        private readonly WEXSymbolMapper _symbolMapper;
        private readonly FixConfiguration _fixConfiguration;
        private readonly IFixBrokerageController _fixBrokerageController;

        private readonly ConcurrentDictionary<SessionID, IWEXFixSessionHandler> _sessionHandlers = new ConcurrentDictionary<SessionID, IWEXFixSessionHandler>();

        private int _expectedMsgSeqNumLogOn = default;

        public WEXFixProtocolDirector(
            WEXSymbolMapper symbolMapper,
            FixConfiguration fixConfiguration,
            IFixBrokerageController fixBrokerageController)
        {
            _symbolMapper = symbolMapper;
            _fixConfiguration = fixConfiguration;
            _fixBrokerageController = fixBrokerageController;
        }

        public IMessageFactory MessageFactory { get; } = new MessageFactory();

        public bool AreSessionsReady()
        {
            return _sessionHandlers.IsEmpty ? false : _sessionHandlers.Values.All(handler => handler.IsReady);
        }

        public void EnrichOutbound(Message msg)
        {
            switch (msg)
            {
                case Logon logon:
                    logon.Header.SetField(new MsgSeqNum(_expectedMsgSeqNumLogOn == 0 ? 1 : _expectedMsgSeqNumLogOn));
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
            handler.IsReady = true;
            _sessionHandlers[sessionId] = handler;

            // Crutch: to logOn with correct MsgSeqNum: Reset Value
            if (_expectedMsgSeqNumLogOn != 0)
                _expectedMsgSeqNumLogOn = 0;
        }

        public void OnLogout(SessionID sessionId)
        {
            Logging.Log.Trace($"OnLogout(): Removing handler for SessionId: {sessionId}");

            if (_sessionHandlers.TryRemove(sessionId, out var handler))
            {
                if (sessionId.SenderCompID == _fixConfiguration.SenderCompId && sessionId.TargetCompID == _fixConfiguration.TargetCompId)
                {
                    _fixBrokerageController.Unregister((IFixOutboundBrokerageHandler)handler);
                }
            }
        }

        public void HandleAdminMessage(Message msg)
        {
            switch (msg)
            {
                case Logout logout:
                    _expectedMsgSeqNumLogOn = GetExpectedMsgSeqNum(msg);
                    break;
                case Heartbeat heartbeat:
                    Logging.Log.Trace($"{msg.GetType().Name}: {msg}");
                    break;
            }
        }

        private IWEXFixSessionHandler CreateSessionHandler(string senderCompId, string targetCompId, ISession session)
        {
            if (senderCompId == _fixConfiguration.SenderCompId && targetCompId == _fixConfiguration.TargetCompId)
            {
                return new WEXOrderRoutingSessionHandler(_symbolMapper, session, _fixBrokerageController, _fixConfiguration);
            }

            throw new Exception($"Unknown session senderCompId: '{senderCompId}'");
        }

        private int GetExpectedMsgSeqNum(Message msg)
        {
            if (!msg.IsSetField(Text.TAG))
                return 0;

            var textMsg = msg.GetString(Text.TAG);
            Logging.Log.Trace($"WEX:logout: TAG<58>,text msg: {textMsg}");
            return textMsg.Contains("expected") ? Int32.Parse(System.Text.RegularExpressions.Regex.Match(textMsg, @"(?<=expected\s)[0-9]+").Value) : 0;
        }
    }
}
