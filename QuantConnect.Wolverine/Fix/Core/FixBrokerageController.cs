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
using QuantConnect.Wolverine.Fix.Protocol;
using QuantConnect.Wolverine.Fix.Utils;
using QuickFix.Fields;
using QuickFix.FIX42;
using System.Collections.Concurrent;

namespace QuantConnect.Wolverine.Fix.Core
{
    public class FixBrokerageController : IFixBrokerageController
    {
        private readonly ConcurrentDictionary<string, ExecutionReport> _orders = new ConcurrentDictionary<string, ExecutionReport>();

        private IFixOutboundBrokerageHandler _handler;

        public event EventHandler<ExecutionReport> ExecutionReport;

        public FixBrokerageController() { }

        public bool CancelOrder(Order order)
        {
            return _handler.CancelOrder(order);
        }

        public List<Order> GetOpenOrders()
        {
            throw new NotImplementedException();
        }

        public void OnOpenOrdersReceived()
        {
            throw new NotImplementedException();
        }

        public bool PlaceOrder(Order order)
        {
            return _handler.PlaceOrder(order);
        }

        public void Receive(ExecutionReport execution)
        {
            if (execution == null)
            {
                throw new ArgumentNullException(nameof(execution));
            }

            var orderId = execution.ClOrdID.getValue();
            var orderStatus = execution.OrdStatus.getValue();
            if (orderStatus != OrdStatus.REJECTED)
            {
                _orders[orderId] = execution;
            }
            else
            {
                _orders.TryRemove(orderId, out _);
            }

            ExecutionReport?.Invoke(this, execution);
        }

        public void Register(IFixOutboundBrokerageHandler handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (_handler != null)
            {
                throw new Exception(
                    $"A handler has already been registered: {_handler.GetType().FullName}#{_handler.GetHashCode()}, received: {handler.GetType().FullName}#{handler.GetHashCode()}");
            }

            _handler = handler;
        }

        public bool RequestOpenOrders()
        {
            throw new NotImplementedException();
        }

        public void Unregister(IFixOutboundBrokerageHandler handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (_handler == null || handler != _handler)
            {
                throw new Exception(
                    $"The handler has not been registered: {handler.GetType().FullName}#{handler.GetHashCode()}");
            }

            _handler = null;
        }

        public bool UpdateOrder(Order order)
        {
            return _handler.UpdateOrder(order);
        } 
    }
}
