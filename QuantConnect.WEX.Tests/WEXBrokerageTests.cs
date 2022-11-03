using QuantConnect.Configuration;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Logging;
using QuantConnect.Securities;
using QuantConnect.Tests.Brokerages;
using QuantConnect.WEX.Fix;
using QuantConnect.WEX.Fix.Core;
using QuantConnect.WEX.Wex;
using QuickFix;
using Log = QuantConnect.Logging.Log;

namespace QuantConnect.WEX.Tests
{
    [TestFixture]
    public partial class WEXBrokerageTests
    {
        private readonly AggregationManager _aggregationManager = new AggregationManager();

        private readonly FixConfiguration _fixConfiguration = new FixConfiguration
        {
            SenderCompId = Config.Get("wex-sender-comp-id"),
            TargetCompId = Config.Get("wex-target-comp-id"),
            Host = Config.Get("wex-host"),
            Port = Config.Get("wex-port"),
            OnBehalfOfCompID = Config.Get("wex-on-behalf-Of-comp-id")
        };

        private readonly Symbol _symbolIBMEquity = Symbol.Create("IBM", SecurityType.Equity, Market.USA);

        [Test]
        public void LogOnFixInstance()
        {
            var marketDataController = new FixMarketDataController();
            var fixProtocolDirector = new WEXFixProtocolDirector(_fixConfiguration, marketDataController);

            using var fixInstance = new FixInstance(fixProtocolDirector, _fixConfiguration, true);

            fixInstance.Initialise();

            var sessionId = new SessionID(_fixConfiguration.FixVersionString, _fixConfiguration.SenderCompId, _fixConfiguration.TargetCompId);

            Thread.Sleep(60000);

            fixInstance.OnLogout(sessionId);

            fixInstance.OnLogon(sessionId);

            fixInstance.Terminate();
        }

        [Test]
        public void SubscribeBrokerage()
        {
            using (var brokerage = new WEXBrokerage(_aggregationManager, _fixConfiguration, true))
            {
                brokerage.Connect();
                Assert.IsTrue(brokerage.IsConnected);

                var dataConfig = new SubscriptionDataConfig(
                typeof(Tick),
                _symbolIBMEquity,
                Resolution.Tick,
                TimeZones.Utc,
                TimeZones.Utc,
                true,
                true,
                false);

                var cts = new CancellationTokenSource();
                ProcessFeed(
                    brokerage.Subscribe(dataConfig, (s, e) => { }),
                    cts,
                    (tick) => {
                        if (tick != null)
                        {
                            Log.Trace("{0}: {1} - {2} / {3}", tick.Time.ToStringInvariant("yyyy-MM-dd HH:mm:ss.fff"), tick.Symbol, (tick as Tick)?.BidPrice, (tick as Tick)?.AskPrice);
                        }
                    });

                Thread.Sleep(20000);

                //brokerage.Unsubscribe(dataConfig);

                Thread.Sleep(5000);

                cts.Cancel();

                brokerage.Disconnect();
                Assert.IsFalse(brokerage.IsConnected);
            }
        }

        private static void ProcessFeed(IEnumerator<BaseData> enumerator, CancellationTokenSource cts, Action<BaseData> callback = null)
        {
            Task.Factory.StartNew(() =>
            {
                try
                {
                    while (enumerator.MoveNext() && !cts.IsCancellationRequested)
                    {
                        var tick = enumerator.Current;
                        callback?.Invoke(tick);
                    }
                }
                catch (AssertionException)
                {
                    throw;
                }
                catch (Exception err)
                {
                    Console.WriteLine(err.Message);
                }
            }, cts.Token);
        }
    }
}
