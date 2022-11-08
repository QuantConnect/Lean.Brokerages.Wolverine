using QuickFix;

namespace QuantConnect.WEX.Fix
{
    public class FixConfiguration
    {
        public string FixVersionString { get; set; } = "FIX.4.2";

        // market data session
        public string SenderCompId { get; set; }
        public string TargetCompId { get; set; }
        public string Host { get; set; }
        public string Port { get; set; }
        public string OnBehalfOfCompID { get; set; } // Fix Protocl tag 115
        public string Account { get; set; }

        public SessionSettings GetDefaultSessionSettings()
        {
            var settings = new SessionSettings();

            var defaultDic = new Dictionary();
            defaultDic.SetString("ConnectionType", "initiator");
            defaultDic.SetString("ReconnectInterval", "30");
            defaultDic.SetString("FileStorePath", @"store");
            defaultDic.SetString("FileLogPath", "log");
            defaultDic.SetString("StartTime", "00:00:00");
            defaultDic.SetString("EndTime", "00:00:00");
            defaultDic.SetBool("UseDataDictionary", true);
            defaultDic.SetString("DataDictionary", @"WEX/WEX-FIX42.xml");
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
