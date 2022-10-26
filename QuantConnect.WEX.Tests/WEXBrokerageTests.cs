using QuantConnect.Configuration;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Logging;
using QuantConnect.Securities;
using QuantConnect.Tests.Brokerages;
using QuantConnect.WEX.Fix;

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
            Port = Config.Get("wex-port")
        };

        private readonly Symbol _symbolEs = Symbol.CreateFuture("ES", Market.CME, new DateTime(2021, 3, 19));

        [Test]
        public void LogOn()
        {
            using (var brokerage = new WEXBrokerage(_aggregationManager, _fixConfiguration, false))
            {
                brokerage.Connect();
                Assert.IsTrue(brokerage.IsConnected);

                var dataConfig = new SubscriptionDataConfig(
                typeof(Tick),
                _symbolEs,
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

                brokerage.Unsubscribe(dataConfig);

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
