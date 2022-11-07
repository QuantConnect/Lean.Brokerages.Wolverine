using QuantConnect.Orders;
using QuickFix.FIX42;
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

        public static OrderStatus ConvertOrderStatus(ExecutionReport execution)
        {
            var execType = execution.ExecType.getValue();
            if (execType == QF.ExecType.ORDER_STATUS)
            {
                execType = execution.OrdStatus.getValue();
            }

            switch (execType)
            {
                case QF.ExecType.NEW:
                    return OrderStatus.Submitted;

                case QF.ExecType.CANCELLED:
                    return OrderStatus.Canceled;

                case QF.ExecType.REPLACED:
                    return OrderStatus.UpdateSubmitted;

                case QF.ExecType.PARTIAL_FILL:
                    return OrderStatus.PartiallyFilled;

                case QF.ExecType.FILL:
                    return OrderStatus.Filled;

                case QF.ExecType.TRADE:
                    return execution.CumQty.getValue() < execution.OrderQty.getValue()
                        ? OrderStatus.PartiallyFilled
                        : OrderStatus.Filled;

                default:
                    return OrderStatus.Invalid;
            }
        }

        public static OrderType ConvertOrderType(char orderType)
        {
            switch (orderType)
            {
                case QF.OrdType.MARKET:
                    return OrderType.Market;

                case QF.OrdType.LIMIT:
                    return OrderType.Limit;

                case QF.OrdType.STOP:
                    return OrderType.StopMarket;

                case QF.OrdType.STOP_LIMIT:
                    return OrderType.StopLimit;

                default:
                    throw new NotSupportedException($"Unsupported order type: {orderType}");
            }
        }

        public static TimeInForce ConvertTimeInForce(char timeInForce)
        {
            switch (timeInForce)
            {
                case QF.TimeInForce.GOOD_TILL_CANCEL:
                    return TimeInForce.GoodTilCanceled;

                case QF.TimeInForce.DAY:
                    return TimeInForce.Day;

                default:
                    throw new NotSupportedException($"Unsupported TimeInForce: {timeInForce}");
            }
        }
    }
}
