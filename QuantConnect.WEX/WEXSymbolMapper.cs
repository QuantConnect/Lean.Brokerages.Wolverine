using QuantConnect.Brokerages;

namespace QuantConnect.WEX
{
    public class WEXSymbolMapper : ISymbolMapper
    {
        // WEX SecurityExchange -> Lean market
        private readonly Dictionary<string, string> _mapSecurityExchangeToLeanMarket = new Dictionary<string, string>
        {
            { "ICE", Market.ICE }
        };

        // WEX SecurityType -> LEAN security type
        private readonly Dictionary<string, SecurityType> _mapSecurityTypeToLeanSecurityType = new Dictionary<string, SecurityType>
        {
            { QuickFix.Fields.SecurityType.COMMON_STOCK, SecurityType.Equity },
            { QuickFix.Fields.SecurityType.FUTURE, SecurityType.Future },
            { QuickFix.Fields.SecurityType.OPTION, SecurityType.Option }
        };

        // LEAN security type -> WEX Security TYpe
        private readonly Dictionary<SecurityType, string> _mapLeanSecurityTypeToSecurityType;

        public WEXSymbolMapper()
        {
            _mapLeanSecurityTypeToSecurityType = _mapSecurityTypeToLeanSecurityType
                .ToDictionary(x => x.Value, x => x.Key);
        }

        public string GetBrokerageSymbol(Symbol symbol)
        {
            return symbol.ID.Symbol;
        }

        public Symbol GetLeanSymbol(string brokerageSymbol, SecurityType securityType, string market, DateTime expirationDate = default, decimal strike = 0, OptionRight optionRight = OptionRight.Call)
        {
            throw new NotImplementedException();
        }

        public string GetBrokerageSecurityType(SecurityType leanSecurityType)
        {
            if (!_mapLeanSecurityTypeToSecurityType.TryGetValue(leanSecurityType, out var securityTypeBrokerage))
            {
                throw new NotSupportedException($"Unsupported LEAN security type: {leanSecurityType}");
            }

            return securityTypeBrokerage;
        }
    }
}
