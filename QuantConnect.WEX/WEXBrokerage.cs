using QuantConnect.Data;
using QuantConnect.Util;
using QuantConnect.Orders;
using QuantConnect.Packets;
using QuantConnect.Interfaces;
using QuantConnect.Securities;
using QuantConnect.Brokerages;
using QuantConnect.WEX.Fix;
using QuantConnect.WEX.Wex;
using QuantConnect.WEX.Fix.Core;
using QuickFix.FIX42;
using QuantConnect.Logging;
using QuantConnect.Orders.Fees;

namespace QuantConnect.WEX
{
    [BrokerageFactory(typeof(WEXBrokerageFactory))]
    public class WEXBrokerage : Brokerage, IDataQueueHandler, IDataQueueUniverseProvider
    {
        private readonly IAlgorithm _algorithm;
        private readonly LiveNodePacket _job;
        private readonly IOrderProvider _orderProvider;
        private readonly IDataAggregator _aggregator;

        private readonly EventBasedDataQueueHandlerSubscriptionManager _subscriptionManager;

        private readonly IFixBrokerageController _fixBrokerageController;
        private readonly FixInstance _fixInstance;
        private readonly WEXSymbolMapper _symbolMapper;

        /// <summary>
        /// Returns true if we're currently connected to the broker
        /// </summary>
        public override bool IsConnected => _fixInstance.IsConnected();

        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="aggregator">consolidate ticks</param>
        public WEXBrokerage(
            IAlgorithm algorithm, 
            LiveNodePacket job, 
            IOrderProvider orderProvider, 
            IDataAggregator aggregator, 
            FixConfiguration fixConfiguration, 
            bool logFixMessages) : base("WEX")
        {
            _job = job;
            _algorithm = algorithm;
            _aggregator = aggregator;
            _orderProvider = orderProvider;

            _symbolMapper = new WEXSymbolMapper();

            _fixBrokerageController = new FixBrokerageController(_symbolMapper);
            _fixBrokerageController.ExecutionReport += OnExecutionReport;

            var fixProtocolDirector = new WEXFixProtocolDirector(_symbolMapper, fixConfiguration, _fixBrokerageController);

            _fixInstance = new FixInstance(fixProtocolDirector, fixConfiguration, logFixMessages);

        }

        #region IDataQueueHandler

        /// <summary>
        /// Subscribe to the specified configuration
        /// </summary>
        /// <param name="dataConfig">defines the parameters to subscribe to a data feed</param>
        /// <param name="newDataAvailableHandler">handler to be fired on new data available</param>
        /// <returns>The new enumerator for this subscription request</returns>
        public IEnumerator<BaseData> Subscribe(SubscriptionDataConfig dataConfig, EventHandler newDataAvailableHandler)
        {
            if (!CanSubscribe(dataConfig.Symbol))
            {
                return Enumerable.Empty<BaseData>().GetEnumerator();
            }

            var enumerator = _aggregator.Add(dataConfig, newDataAvailableHandler);
            _subscriptionManager.Subscribe(dataConfig);

            return enumerator;
        }

        /// <summary>
        /// Removes the specified configuration
        /// </summary>
        /// <param name="dataConfig">Subscription config to be removed</param>
        public void Unsubscribe(SubscriptionDataConfig dataConfig)
        {
            _subscriptionManager.Unsubscribe(dataConfig);
            _aggregator.Remove(dataConfig);
        }

        /// <summary>
        /// Sets the job we're subscribing for
        /// </summary>
        /// <param name="job">Job we're subscribing for</param>
        public void SetJob(LiveNodePacket job)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Brokerage

        /// <summary>
        /// Gets all open orders on the account.
        /// NOTE: The order objects returned do not have QC order IDs.
        /// </summary>
        /// <returns>The open orders returned from IB</returns>
        public override List<Order> GetOpenOrders()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets all holdings for the account
        /// </summary>
        /// <returns>The current holdings from the account</returns>
        public override List<Holding> GetAccountHoldings()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the current cash balance for each currency held in the brokerage account
        /// </summary>
        /// <returns>The current cash balance for each currency available for trading</returns>
        public override List<CashAmount> GetCashBalance()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Places a new order and assigns a new broker ID to the order
        /// </summary>
        /// <param name="order">The order to be placed</param>
        /// <returns>True if the request for a new order has been placed, false otherwise</returns>
        public override bool PlaceOrder(Order order)
        {
            return _fixBrokerageController.PlaceOrder(order);
        }

        /// <summary>
        /// Updates the order with the same id
        /// </summary>
        /// <param name="order">The new order information</param>
        /// <returns>True if the request was made for the order to be updated, false otherwise</returns>
        public override bool UpdateOrder(Order order)
        {
            return _fixBrokerageController.UpdateOrder(order);
        }

        /// <summary>
        /// Cancels the order with the specified ID
        /// </summary>
        /// <param name="order">The order to cancel</param>
        /// <returns>True if the request was made for the order to be canceled, false otherwise</returns>
        public override bool CancelOrder(Order order)
        {
            return _fixBrokerageController.CancelOrder(order);
        }

        /// <summary>
        /// Connects the client to the broker's remote servers
        /// </summary>
        public override void Connect()
        {
            _fixInstance.Initialise();
        }

        /// <summary>
        /// Disconnects the client from the broker's remote servers
        /// </summary>
        public override void Disconnect()
        {
            throw new NotImplementedException();
        }

        #endregion

        #region IDataQueueUniverseProvider

        /// <summary>
        /// Method returns a collection of Symbols that are available at the data source.
        /// </summary>
        /// <param name="symbol">Symbol to lookup</param>
        /// <param name="includeExpired">Include expired contracts</param>
        /// <param name="securityCurrency">Expected security currency(if any)</param>
        /// <returns>Enumerable of Symbols, that are associated with the provided Symbol</returns>
        public IEnumerable<Symbol> LookupSymbols(Symbol symbol, bool includeExpired, string securityCurrency = null)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns whether selection can take place or not.
        /// </summary>
        /// <remarks>This is useful to avoid a selection taking place during invalid times, for example IB reset times or when not connected,
        /// because if allowed selection would fail since IB isn't running and would kill the algorithm</remarks>
        /// <returns>True if selection can take place</returns>
        public bool CanPerformSelection()
        {
            throw new NotImplementedException();
        }

        #endregion

        private bool CanSubscribe(Symbol symbol)
        {
            if (symbol.Value.IndexOfInvariant("universe", true) != -1)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Adds the specified symbols to the subscription
        /// </summary>
        /// <param name="symbols">The symbols to be added keyed by SecurityType</param>
        private bool Subscribe(IEnumerable<Symbol> symbols)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Removes the specified symbols to the subscription
        /// </summary>
        /// <param name="symbols">The symbols to be removed keyed by SecurityType</param>
        private bool Unsubscribe(IEnumerable<Symbol> symbols)
        {
            throw new NotImplementedException();
        }

        private void OnExecutionReport(object sender, ExecutionReport e)
        {
            Log.Trace($"WexBrokerage:OnExecutionReport(): {sender}");

            OrderStatus orderStatus = default;

            var orderId = orderStatus == OrderStatus.Canceled || orderStatus == OrderStatus.UpdateSubmitted
                ? e.OrigClOrdID.getValue()
                : e.ClOrdID.getValue();
            var time = e.TransactTime.getValue();

            var order = _orderProvider.GetOrderByBrokerageId(orderId);

            if (order == null)
            {
                Log.Error($"TradingTechnologiesBrokerage.OnExecutionReport(): Unable to locate order with BrokerageId: {orderId}");
                return;
            }

            var message = "WEX Order Event";
            if (e.IsSetText())
            {
                message += $" - {e.Text.getValue()}";
            }

            var orderEvent = new OrderEvent(order, time, OrderFee.Zero, message)
            {
                Status = orderStatus
            };

            if (orderStatus == OrderStatus.Filled || orderStatus == OrderStatus.PartiallyFilled)
            {
                var displayFactor = 0;

                var filledQuantity = e.LastShares.getValue();
                var remainingQuantity = order.AbsoluteQuantity - e.CumQty.getValue();

                orderEvent.FillQuantity = filledQuantity * (order.Direction == OrderDirection.Buy ? 1 : -1);
                orderEvent.FillPrice = e.LastPx.getValue() * displayFactor;

                if (remainingQuantity > 0)
                {
                    orderEvent.Message += " - " + remainingQuantity + " remaining";
                }
            }

            OnOrderEvent(orderEvent);
        }
    }
}
