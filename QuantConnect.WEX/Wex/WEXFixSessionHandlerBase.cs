using QuickFix;
using QuickFix.FIX42;

namespace QuantConnect.WEX.Wex
{
    public abstract class WEXFixSessionHandlerBase : MessageCracker, IWEXFixSessionHandler
    {
        public bool IsReady { get; protected set; }

        public void OnMessage(News news, SessionID sessionId)
        {
            var headline = news.IsSetHeadline() ? news.Headline.getValue() : "<no-headline>";
            Logging.Log.Trace("[{0}] OnMessage: {1} = {2}: {3}", sessionId, news.GetType().Name, headline, news.IsSetField(58) ? news.RawData.getValue() : "<no-text>");

            if (string.Equals(headline, "Recovery Complete", StringComparison.InvariantCultureIgnoreCase))
            {
                OnRecoveryCompleted();
            }
        }

        protected virtual void OnRecoveryCompleted() { }

        public void OnMessage(BusinessMessageReject msg, SessionID sessionId)
        {
            //var reason = msg.BusinessRejectReason..DescribeInt(msg.IsSetBusinessRejectReason());
            var reason = msg.BusinessRejectReason.toStringField();
            Logging.Log.Error("[{0}] {1}: {2}: {3}", sessionId, msg.GetType().Name, reason, msg.IsSetText() ? msg.Text.getValue() : "<none>");
        }
    }
}
