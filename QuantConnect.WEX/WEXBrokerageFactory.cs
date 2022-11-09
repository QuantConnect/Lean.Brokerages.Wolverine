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

using QuantConnect.Packets;
using QuantConnect.Brokerages;
using QuantConnect.Interfaces;
using QuantConnect.Securities;
using QuantConnect.Configuration;
using QuantConnect.WEX.Fix;
using QuantConnect.Util;
using QuantConnect.Data;

namespace QuantConnect.WEX
{
    /// <summary>
    /// Provides a WEX Brokerage implementation of BrokerageFactory
    /// </summary>
    public class WEXBrokerageFactory : BrokerageFactory
    {
        /// <summary>
        /// Gets the brokerage data required to run the brokerage from configuration/disk
        /// </summary>
        /// <remarks>
        /// The implementation of this property will create the brokerage data dictionary required for
        /// running live jobs. See <see cref="IJobQueueHandler.NextJob"/>
        /// </remarks>
        public override Dictionary<string, string> BrokerageData => new Dictionary<string, string>
        {
            { "wex-sender-comp-id", Config.Get("wex-sender-comp-id") },
            { "wex-target-comp-id", Config.Get("wex-target-comp-id") },
            { "wex-host", Config.Get("wex-host") },
            { "wex-port", Config.Get("wex-port") },
            { "wex-on-behalf-of-comp-id", Config.Get("wex-on-behalf-of-comp-id") },
            { "wex-account", Config.Get("wex-account") },
            { "wex-log-fix-messages", Config.Get("tt-log-fix-messages") }
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="WEXBrokerageFactory"/> class
        /// </summary>
        public WEXBrokerageFactory() : base(typeof(WEXBrokerage))
        {
        }

        /// <summary>
        /// Gets a new instance of the <see cref="DefaultBrokerageModel"/>
        /// </summary>
        /// <param name="orderProvider">The order provider</param>
        public override IBrokerageModel GetBrokerageModel(IOrderProvider orderProvider) => new DefaultBrokerageModel();

        /// <summary>
        /// Creates a new IBrokerage instance
        /// </summary>
        /// <param name="job">The job packet to create the brokerage for</param>
        /// <param name="algorithm">The algorithm instance</param>
        /// <returns>A new brokerage instance</returns>
        public override IBrokerage CreateBrokerage(LiveNodePacket job, IAlgorithm algorithm)
        {
            var errors = new List<string>();

            // read values from the brokerage data
            var fixConfiguration = new FixConfiguration
            {
                Host = Read<string>(job.BrokerageData, "wex-host", errors),
                Port = Read<string>(job.BrokerageData, "wex-port", errors),

                SenderCompId = Read<string>(job.BrokerageData, "wex-sender-comp-id", errors),
                TargetCompId = Read<string>(job.BrokerageData, "wex-target-comp-id", errors),

                Account = Read<string>(job.BrokerageData, "wex-account", errors),
                OnBehalfOfCompID = Read<string>(job.BrokerageData, "wex-on-behalf-of-comp-id", errors)
            };

            var logFixMessages = Read<bool>(job.BrokerageData, "wex-log-fix-messages", errors);

            if (errors.Count != 0)
            {
                // if we had errors then we can't create the instance
                throw new Exception(string.Join(Environment.NewLine, errors));
            }

            var instance = new WEXBrokerage(
                algorithm,
                job,
                algorithm.Transactions,                
                fixConfiguration,
                logFixMessages);

            Composer.Instance.AddPart<IDataQueueHandler>(instance);

            return instance;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public override void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
