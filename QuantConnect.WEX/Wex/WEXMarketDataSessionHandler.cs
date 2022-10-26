using NodaTime;
using QuantConnect.WEX.Fix.Core;
using QuantConnect.WEX.Fix.Protocol;
using QuickFix;
using QuickFix.Fields;
using QuickFix.FIX42;
using System.Collections.Concurrent;
using System.Globalization;

namespace QuantConnect.WEX.Wex
{
    public class WEXMarketDataSessionHandler : WEXFixSessionHandlerBase, IFixOutboundMarketDataHandler
    {
        private readonly ISession _session;
        private readonly IFixMarketDataController _fixMarketDataController;
        private int _nextRequestId;

        private readonly ConcurrentDictionary<string, SubscriptionEntry> _subscriptions = new ConcurrentDictionary<string, SubscriptionEntry>();

        // exchange time zones by symbol
        private readonly Dictionary<Symbol, DateTimeZone> _symbolExchangeTimeZones = new Dictionary<Symbol, DateTimeZone>();

        public WEXMarketDataSessionHandler(ISession session, IFixMarketDataController fixMarketDataController)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _fixMarketDataController = fixMarketDataController ?? throw new ArgumentNullException(nameof(fixMarketDataController));
            _fixMarketDataController.Register(this);
        }

        protected override void OnRecoveryCompleted()
        {
            IsReady = true;
        }

        public void OnMessage(MarketDataSnapshotFullRefresh marketDataSnapshot, SessionID _)
        {
            throw new NotImplementedException();
        }

        public void OnMessage(MarketDataIncrementalRefresh incrementalRefresh, SessionID _)
        {
            throw new NotImplementedException();
        }

        private void ProcessUpdate(string requestId, MarketDataIncrementalRefresh.NoMDEntriesGroup group)
        {
            throw new NotImplementedException();
        }

        public bool SubscribeToSymbol(Symbol symbol)
        {
            var requestId = Interlocked.Increment(ref _nextRequestId).ToString(CultureInfo.InvariantCulture);

            var ticker = symbol.ID.Symbol;

            //var securityType = new QuantConnect.Fix.TT.FIX44.Fields.SecurityType(_symbolMapper.GetBrokerageProductType(symbol.SecurityType));
            var securityType = new QuickFix.Fields.SecurityType("CS");
            var securityExchange = new SecurityExchange(symbol.ID.Symbol);

            Logging.Log.Trace($"Subscribing to: {ticker}-{symbol.Value}-{securityType}-{securityExchange.getValue()}, RequestId: {requestId}");

            var marketDataRequest = new MarketDataRequest
            {
                MDReqID = new MDReqID(requestId),
                SubscriptionRequestType = new SubscriptionRequestType(SubscriptionRequestType.SNAPSHOT_PLUS_UPDATES),
                MDUpdateType = new MDUpdateType(MDUpdateType.INCREMENTAL_REFRESH),
                
                //IncludeQuotes = new IncludeQuotes(true),
                //IncludeNumberOfOrders = new IncludeNumberOfOrders(IncludeNumberOfOrders.YES),
                MarketDepth = new MarketDepth(1)
            };

            // Add fields
            //marketDataRequest.AddGroup<MarketDataRequest.NoMDEntryTypesGroup, MDEntryType, TTMarketDataType>();

            // Add symbols
            var symbolsGroup = new MarketDataRequest.NoRelatedSymGroup
            {
                Symbol = new QuickFix.Fields.Symbol(ticker),
                SecurityType = securityType,
                SecurityExchange = securityExchange
            };

            marketDataRequest.AddGroup(symbolsGroup);

            _subscriptions.TryAdd(requestId, new SubscriptionEntry { Symbol = symbol });

            return _session.Send(marketDataRequest);
        }

        public bool UnsubscribeFromSymbol(Symbol symbol)
        {
            throw new NotImplementedException();
        }

        private class SubscriptionEntry
        {
            public Symbol Symbol { get; set; }

            public decimal BidPrice { get; set; }
            public decimal BidSize { get; set; }

            public decimal AskPrice { get; set; }
            public decimal AskSize { get; set; }

            public decimal LastPrice { get; set; }
            public decimal LastSize { get; set; }
        }
    }
}
