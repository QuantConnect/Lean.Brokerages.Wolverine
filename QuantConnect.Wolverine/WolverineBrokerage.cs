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

using QuantConnect.Util;
using QuantConnect.Orders;
using QuantConnect.Packets;
using QuantConnect.Interfaces;
using QuantConnect.Securities;
using QuickFix.FIX42;
using QuantConnect.Brokerages.Fix;

namespace QuantConnect.Brokerages.Wolverine
{
    [BrokerageFactory(typeof(WolverineBrokerageFactory))]
    public class WolverineBrokerage : FixBrokerage
    {
        private readonly ISecurityProvider _securityProvider;
        private readonly WolverineSymbolMapper _symbolMapper;

        protected override string DataDictionaryFilePath => "Wolverine-FIX42.xml";

        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="aggregator">consolidate ticks</param>
        public WolverineBrokerage(
            IAlgorithm algorithm,
            LiveNodePacket job,
            IOrderProvider orderProvider,
            FixConfiguration fixConfiguration,
            ISecurityProvider securityProvider) : base(fixConfiguration, orderProvider, algorithm, job, "Wolverine")
        {
            _securityProvider = securityProvider;
            var mapFileProvider = Composer.Instance.GetPart<IMapFileProvider>();
            _symbolMapper = new WolverineSymbolMapper(mapFileProvider);

            InitializeFix(new WolverineOrderRoutingSessionHandler(_symbolMapper, fixConfiguration.Account, _securityProvider));
            ValidateSubscription(221);
        }

        protected override void OnExecutionReport(object sender, ExecutionReport e)
        {
            var orderStatus = Fix.Utility.ConvertOrderStatus(e);

            var orderId = orderStatus == OrderStatus.Canceled || orderStatus == OrderStatus.CancelPending || orderStatus == OrderStatus.UpdateSubmitted
                ? e.OrigClOrdID.getValue()
                : e.ClOrdID.getValue();

            OnExecutionReport(orderId, e);
        }
    }
}
