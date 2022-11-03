using QuantConnect.Orders;
using QF = QuickFix.Fields;

namespace QuantConnect.WEX.Fix.Utils
{
    public static class Utility
    {
        /// <summary>
        /// When we use SecurityType.Option we should round StrikePrice
        /// </summary>
        public const int LIMIT_DECIMAL_PLACE = 3;

        public static QF.MaturityMonthYear GetMaturityMonthYear(Symbol symbol)
        {
            if (symbol.SecurityType != SecurityType.Option)
            {
                throw new NotSupportedException("GetMaturityMonthYear() can only be called for the Option security type.");
            }

            var ticker = SymbolRepresentation.GenerateFutureTicker(symbol.ID.Symbol, symbol.ID.Date);
            var properties = SymbolRepresentation.ParseFutureTicker(ticker);

            var maturity = $"{2000 + properties.ExpirationYearShort:D4}{properties.ExpirationMonth:D2}";

            return new QF.MaturityMonthYear(maturity);
        }

        public static QF.TimeInForce ConvertTimeInForce(TimeInForce timeInForce, OrderType orderType)
        {
            if (timeInForce == TimeInForce.GoodTilCanceled)
            {
                if (orderType == OrderType.Market)
                {
                    // some exchanges do not accept GTC with market orders
                    return new QF.TimeInForce(QF.TimeInForce.DAY);
                }

                return new QF.TimeInForce(QF.TimeInForce.GOOD_TILL_CANCEL);
            }

            if (timeInForce == TimeInForce.Day)
            {
                return new QF.TimeInForce(QF.TimeInForce.DAY);
            }

            throw new NotSupportedException($"Unsupported TimeInForce: {timeInForce.GetType().Name}");
        }
    }
}
