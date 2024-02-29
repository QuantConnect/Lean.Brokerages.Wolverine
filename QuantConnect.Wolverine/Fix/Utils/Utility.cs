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

using QuantConnect.Orders;
using QuickFix.FIX42;
using QF = QuickFix.Fields;

namespace QuantConnect.Brokerages.Wolverine.Fix.Utils
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

                case QF.ExecType.PENDING_CANCEL:
                    return OrderStatus.CancelPending;

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
    }
}
