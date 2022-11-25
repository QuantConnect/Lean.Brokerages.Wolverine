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

using QuantConnect.Tests;

namespace QuantConnect.Wolverine.Tests
{
    [TestFixture]
    [Explicit("These tests require a valid WEX configuration.")]
    public partial class WolverineBrokerageTests
    {
        private readonly QCAlgorithm _algorithm = new QCAlgorithm();
        private readonly LiveNodePacket _job = new LiveNodePacket();

        private readonly OrderProvider _orderProvider = new OrderProvider(new List<Order>());

        private readonly FixConfiguration _fixConfiguration = new FixConfiguration
        {
            SenderCompId = Config.Get("wolverine-sender-comp-id"),
            TargetCompId = Config.Get("wolverine-target-comp-id"),
            Host = Config.Get("wolverine-host"),
            Port = Config.Get("wolverine-port"),
            OnBehalfOfCompID = Config.Get("wolverine-on-behalf-of-comp-id"),
            Account = Config.Get("wolverine-account")

        };

        private static readonly Symbol _invalidSymbol = Symbol.Create("XY", SecurityType.Equity, Market.USA);
        private static readonly Symbol _symbolNVAX = Symbol.Create("NVAX", SecurityType.Equity, Market.USA);

        [Test]
        public void ClientConnects()
        {
            using (var brokerage = CreateBrokerage())
            {
                Assert.IsFalse(brokerage.IsConnected);

                brokerage.Connect();
                Assert.IsTrue(brokerage.IsConnected);

                brokerage.Disconnect();
                Assert.IsFalse(brokerage.IsConnected);
            }
        }

        [Test]
        public void GetsAccountHoldings()
        {
            using (var brokerage = CreateBrokerage())
            {
                Assert.IsFalse(brokerage.IsConnected);

                brokerage.Connect();
                Assert.IsTrue(brokerage.IsConnected);

                var holdings = brokerage.GetAccountHoldings();
                foreach (var holding in holdings)
                {
                    Log.Trace($"Holding: {holding}");
                }
                Log.Trace($"Holdings: {holdings.Count}");

                brokerage.Disconnect();
                Assert.IsFalse(brokerage.IsConnected);
            }
        }

        private static object[] _marketOrderTestCases =
{
            // Buy
            new TestCaseData(_symbolNVAX, 1),

            // Sell
            new TestCaseData(_symbolNVAX, -1),
        };

        [TestCaseSource(nameof(_marketOrderTestCases))]
        public void SubmitsMarketOrder(Symbol symbol, int quantity)
        {
            using (var brokerage = CreateBrokerage())
            {
                var submittedEvent = new ManualResetEvent(false);
                var filledEvent = new ManualResetEvent(false);

                brokerage.OrderStatusChanged += (s, e) =>
                {
                    if (e.Status == OrderStatus.Submitted)
                    {
                        submittedEvent.Set();
                    }
                    else if (e.Status == OrderStatus.Filled)
                    {
                        filledEvent.Set();
                    }
                };

                brokerage.Connect();
                Assert.IsTrue(brokerage.IsConnected);

                var order = new MarketOrder(symbol, quantity, DateTime.UtcNow);
                _orderProvider.Add(order);

                Assert.IsTrue(brokerage.PlaceOrder(order));

                Assert.IsTrue(submittedEvent.WaitOne(TimeSpan.FromSeconds(10)));
                Assert.IsTrue(filledEvent.WaitOne(TimeSpan.FromSeconds(10)));
            }
        }

        [Ignore("We cannot test in paper connection")]
        [Test]
        public void SubmitsMarketOrderForInvalidConfiguration()
        {
            var symbol = _symbolNVAX;

            _fixConfiguration.Account = "XYZ";

            using (var brokerage = CreateBrokerage())
            {
                var invalidEvent = new ManualResetEvent(false);

                brokerage.OrderStatusChanged += (s, e) =>
                {
                    if (e.Status == OrderStatus.Invalid)
                    {
                        Assert.That(e.Message.EndsWith($"Invalid account {_fixConfiguration.Account}") ||
                                    e.Message.EndsWith("Trading Technologies Order Event"));

                        invalidEvent.Set();
                    }
                };

                brokerage.Connect();
                Assert.IsTrue(brokerage.IsConnected);

                var order = new MarketOrder(symbol, 1, DateTime.UtcNow);
                _orderProvider.Add(order);

                Assert.IsTrue(brokerage.PlaceOrder(order));

                Assert.IsTrue(invalidEvent.WaitOne(TimeSpan.FromSeconds(5)));
            }
        }

        [Ignore("We cannot test in paper connection")]
        [Test]
        public void SubmitsMarketOrderForInvalidSymbol()
        {
            var symbol = _invalidSymbol;

            using (var brokerage = CreateBrokerage())
            {
                var invalidEvent = new ManualResetEvent(false);

                brokerage.OrderStatusChanged += (s, e) =>
                {
                    if (e.Status == OrderStatus.Invalid)
                    {
                        Assert.That(e.Message.Contains("Lookup by name failed") || e.Message.Contains("No instrument found"));

                        invalidEvent.Set();
                    }
                };

                brokerage.Connect();
                Assert.IsTrue(brokerage.IsConnected);

                var order = new MarketOrder(symbol, 1, DateTime.UtcNow);
                _orderProvider.Add(order);

                Assert.IsTrue(brokerage.PlaceOrder(order));

                Assert.IsTrue(invalidEvent.WaitOne(TimeSpan.FromSeconds(5)));
            }
        }

        [Test]
        public void CanLogonAfterLogout()
        {
            var symbolMapper = new WolverineSymbolMapper(TestGlobals.MapFileProvider);

            var brokerageController = new FixBrokerageController();

            var fixProtocolDirector = new WolverineFixProtocolDirector(symbolMapper, _fixConfiguration, brokerageController, new SecurityProvider());

            using var fixInstance = new FixInstance(fixProtocolDirector, _fixConfiguration, true);

            fixInstance.Initialize();

            var sessionId = new SessionID(_fixConfiguration.FixVersionString, _fixConfiguration.SenderCompId, _fixConfiguration.TargetCompId);

            Thread.Sleep(20000);

            fixInstance.OnLogout(sessionId);

            fixInstance.OnLogon(sessionId);

            Thread.Sleep(20000);

            fixInstance.Terminate();
        }

        private WolverineBrokerage CreateBrokerage()
        {
            return new WolverineBrokerage(_algorithm, _job, _orderProvider, _fixConfiguration, new SecurityProvider(), TestGlobals.MapFileProvider, true);
        }
    }
}
