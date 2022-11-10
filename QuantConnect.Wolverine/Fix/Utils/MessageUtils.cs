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

using QuickFix.Fields;
using System.Collections.Concurrent;
using System.Reflection;

namespace QuantConnect.Wolverine.Fix.Utils
{
    public static class MessageUtils
    {
        private static readonly ConcurrentDictionary<Type, object> DataValues = new ConcurrentDictionary<Type, object>();

        public static string DescribeInt<T>(this T field, bool isSet) where T : IntField, new()
        {
            return field.Describe<T, int>(isSet);
        }

        public static string DescribeChar<T>(this T field, bool isSet) where T : CharField, new()
        {
            return field.Describe<T, char>(isSet);
        }

        private static string Describe<T, TBase>(this T field, bool isSet) where T : FieldBase<TBase>, new()
        {
            if (!isSet)
            {
                return "<unset>";
            }

            if (field == null)
            {
                throw new ArgumentNullException(nameof(field), $"Parameter '{nameof(field)}' was null, despite {nameof(isSet)} being true.");
            }

            IReadOnlyDictionary<T, string> values;
            if (DataValues.TryGetValue(typeof(T), out var v))
            {
                values = (IReadOnlyDictionary<T, string>)v;
            }
            else
            {
                values = GetFieldNames<T, TBase>();
                DataValues[typeof(T)] = values;
            }

            return values.TryGetValue(field, out var r) ? r : $"<unknown:{field.Obj}>";
        }

        private static Dictionary<T, string> GetFieldNames<T, TBase>() where T : FieldBase<TBase>, new()
        {
            return typeof(T).GetFields(BindingFlags.Static ^ BindingFlags.Public)
                .Where(f => f.FieldType == typeof(TBase))
                .GroupBy(f => (TBase)f.GetValue(null))
                .ToDictionary(g => new T { Obj = g.Key }, g => string.Join("/", g.Select(f => f.Name)));
        }
    }
}
