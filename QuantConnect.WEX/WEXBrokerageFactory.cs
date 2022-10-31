﻿using QuantConnect.Packets;
using QuantConnect.Brokerages;
using QuantConnect.Interfaces;
using QuantConnect.Securities;
using QuantConnect.Configuration;

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
            { "wex-on-behalf-Of-comp-id", Config.Get("wex-on-behalf-Of-comp-id") }
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="TemplateBrokerageFactory"/> class
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
            throw new NotImplementedException();
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
