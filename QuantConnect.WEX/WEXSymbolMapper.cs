using QuantConnect.Brokerages;

namespace QuantConnect.WEX
{
    public class WEXSymbolMapper : ISymbolMapper
    {
        public string GetBrokerageSymbol(Symbol symbol)
        {
            return symbol.ID.Symbol;
        }

        public Symbol GetLeanSymbol(string brokerageSymbol, SecurityType securityType, string market, DateTime expirationDate = default, decimal strike = 0, OptionRight optionRight = OptionRight.Call)
        {
            throw new NotImplementedException();
        }
    }
}
