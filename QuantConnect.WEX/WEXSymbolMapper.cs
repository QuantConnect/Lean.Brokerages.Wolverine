﻿/*
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

        public SecurityType GetLeanSecurityType(string productType)
        {
            if (!_mapSecurityTypeToLeanSecurityType.TryGetValue(productType, out var securityType))
            {
                throw new NotSupportedException($"Unsupported TT ProductType: {productType}");
            }

            return securityType;
        }
    }
}
