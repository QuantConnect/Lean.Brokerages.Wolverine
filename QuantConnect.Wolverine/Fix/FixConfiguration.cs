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
using QuantConnect.Configuration;

namespace QuantConnect.Wolverine.Fix
{
    public class FixConfiguration
    {
        private readonly int _maxSenderSessionId = Config.GetInt("max-sender-session-id", 15);

        private int? _senderSessionId = null;

        public string FixVersionString { get; set; } = "FIX.4.2";

        // market data session
        public string SenderCompId { get; set; }
        public string TargetCompId { get; set; }
        public string Host { get; set; }
        public string Port { get; set; }

        /// <summary>
        /// Use in FIX LogOn in header of request 
        /// </summary>
        /// <remarks>Fix Protocl tag 115</remarks>
        public string OnBehalfOfCompID { get; set; }
        public string Account { get; set; }

        public SessionSettings GetDefaultSessionSettings()
        {
            var settings = new SessionSettings();

            var defaultDic = new Dictionary();
            defaultDic.SetString("ConnectionType", "initiator");
            defaultDic.SetString("ReconnectInterval", "5");
            defaultDic.SetString("FileStorePath", @"store");
            defaultDic.SetString("FileLogPath", "log");
            defaultDic.SetString("StartTime", "00:00:00");
            defaultDic.SetString("EndTime", "00:00:00");
            defaultDic.SetBool("UseDataDictionary", true);
            defaultDic.SetString("DataDictionary", @"Wolverine-FIX42.xml");
            defaultDic.SetString("BeginString", FixVersionString);
            defaultDic.SetString("TimeZone", "UTC");
            defaultDic.SetBool("UseLocalTime", false);
            defaultDic.SetBool("SendLogoutBeforeDisconnectFromTimeout", false);
            defaultDic.SetString("HeartBtInt", "30");
            defaultDic.SetString("LogonTimeout", "15");
            defaultDic.SetString("SocketConnectHost", Host);
            defaultDic.SetString("SocketConnectPort", Port);

            settings.Set(defaultDic);

            var sessionId = GetNewSessionId();
            settings.Set(sessionId, new Dictionary());

            return settings;
        }

        /// <summary>
        /// Will create a new session id
        /// </summary>
        /// <remarks>Will increment the sender session Id to find a free connection on the target</remarks>
        private SessionID GetNewSessionId()
        {
            var senderCompId = SenderCompId;
            if (!_senderSessionId.HasValue)
            {
                // the first time we try we directly use the plain 'SenderCompId'
                _senderSessionId = 0;
            }
            else
            {
                // following calls we add an incremental id, we try to find a free connection point
                senderCompId += $"-{_senderSessionId}";
                _senderSessionId++;
                if(_senderSessionId > _maxSenderSessionId)
                {
                    Reset();
                }
            }

            return new SessionID(FixVersionString, senderCompId, TargetCompId);
        }

        public void Reset()
        {
            // start again the next time
            _senderSessionId = null;
        }
    }
}
