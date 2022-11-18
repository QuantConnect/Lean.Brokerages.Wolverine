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

namespace QuantConnect.Wolverine.Fix
{
    public class FixConfiguration
    {
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

            settings.Set(defaultDic);

            var orderRoutingDic = new Dictionary();
            orderRoutingDic.SetString("SenderCompID", SenderCompId);
            orderRoutingDic.SetString("TargetCompID", TargetCompId);
            orderRoutingDic.SetString("SocketConnectHost", Host);
            orderRoutingDic.SetString("SocketConnectPort", Port);

            var orderRoutingSessionId = new SessionID(FixVersionString, SenderCompId, TargetCompId);
            settings.Set(orderRoutingSessionId, orderRoutingDic);

            return settings;
        }
    }
}
