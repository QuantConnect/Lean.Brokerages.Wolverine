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

using QuantConnect.Algorithm;
using QuantConnect.Configuration;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.Packets;
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
        private readonly QCAlgorithm _algorithm = new QCAlgorithm();
        private readonly LiveNodePacket _job = new LiveNodePacket();

        private readonly OrderProvider _orderProvider = new OrderProvider(new List<Order>());

        private readonly FixConfiguration _fixConfiguration = new FixConfiguration
        {
            SenderCompId = Config.Get("wex-sender-comp-id"),
            TargetCompId = Config.Get("wex-target-comp-id"),
            Host = Config.Get("wex-host"),
            Port = Config.Get("wex-port"),
            OnBehalfOfCompID = Config.Get("wex-on-behalf-of-comp-id"),
            Account = Config.Get("wex-account")
        };

        private static readonly Symbol _symbolIBMEquity = Symbol.Create("IBM", SecurityType.Equity, Market.USA);
        private static readonly Symbol _symbolINO = Symbol.Create("INO", SecurityType.Equity, Market.USA);
        private static readonly Symbol _symbolNVAX = Symbol.Create("NVAX", SecurityType.Equity, Market.USA);

        [Test]
        public void LogOnFixInstance()
        {
            var symbolMapper = new WEXSymbolMapper();

            var brokerageController = new FixBrokerageController(symbolMapper);

            var fixProtocolDirector = new WEXFixProtocolDirector(symbolMapper, _fixConfiguration, brokerageController);

            using var fixInstance = new FixInstance(fixProtocolDirector, _fixConfiguration, true);

            fixInstance.Initialise();

            var sessionId = new SessionID(_fixConfiguration.FixVersionString, _fixConfiguration.SenderCompId, _fixConfiguration.TargetCompId);

            Thread.Sleep(40000);

            fixInstance.OnLogout(sessionId);

            fixInstance.OnLogon(sessionId);

            Thread.Sleep(40000);

            fixInstance.Terminate();
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

        private static readonly object[] _limitOrderTestCases =
{
            // Buy below market price
            new TestCaseData(_symbolNVAX, 1, 12.30m),

            // Sell above market price
            new TestCaseData(_symbolNVAX, -1, 25.60m),

            // Buy above market price
            new TestCaseData(_symbolNVAX, 1, 19.60m),

            // Sell below market price
            new TestCaseData(_symbolNVAX, -1, 19.30m),
        };

        [Ignore("The logic hasn't completed yet")]
        [TestCaseSource(nameof(_limitOrderTestCases))]
        public void SubmitsLimitOrder(Symbol symbol, int quantity, decimal limitPriceOffsetTicks)
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

                var limitPrice = limitPriceOffsetTicks;

                var order = new LimitOrder(symbol, quantity, limitPrice, DateTime.UtcNow);
                _orderProvider.Add(order);

                Assert.IsTrue(brokerage.PlaceOrder(order));

                Assert.IsTrue(submittedEvent.WaitOne(TimeSpan.FromSeconds(10)));
            }
        }

        private WEXBrokerage CreateBrokerage()
        {
            return new WEXBrokerage(_algorithm, _job, _orderProvider, _fixConfiguration, true);
        }
    }
}
