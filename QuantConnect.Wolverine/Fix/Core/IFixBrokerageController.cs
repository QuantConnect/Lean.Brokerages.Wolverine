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
using QuickFix.FIX42;

namespace QuantConnect.Wolverine.Fix.Core
{
    /// <summary>
    ///     Controls brokerage related communication between QC and a FIX protocol implementation.
    /// </summary>
    public interface IFixBrokerageController
    {
        event EventHandler<ExecutionReport> ExecutionReport;
        event EventHandler<OrderCancelReject> CancelReject;

        /// <summary>
        ///     Registers a brokerage handler to this controller.
        /// </summary>
        /// <param name="handler">Handler to register</param>
        void Register(IFixOutboundBrokerageHandler handler);

        /// <summary>
        ///     Unregisters a brokerage handler from this controller.
        /// </summary>
        /// <param name="handler">Handler to register</param>
        void Unregister(IFixOutboundBrokerageHandler handler);

        /// <summary>
        ///     Receive an order status update.
        /// </summary>
        /// <param name="orderEvent">Order event</param>
        // TODO: Decide whether communication from a handler back to the controller should be done via an event.
        void Receive(ExecutionReport orderEvent);

        /// <summary>
        /// Receive and order cancellation rejection
        /// </summary>
        /// <param name="reject">The rejection</param>
        void Receive(OrderCancelReject reject);

        bool RequestOpenOrders();

        /// <summary>
        ///     Places an order.
        /// </summary>
        /// <param name="order">The order to submit.</param>
        bool PlaceOrder(Order order);

        /// <summary>
        ///     Updates an existing order.
        /// </summary>
        /// <param name="order">The order to update.</param>
        bool UpdateOrder(Order order);

        /// <summary>
        ///     Returns all orders the brokerage is aware of.
        /// </summary>
        /// <returns>All orders</returns>
        List<Order> GetOpenOrders();

        /// <summary>
        ///     Cancel an order.
        /// </summary>
        /// <param name="order">The order to cancel.</param>
        bool CancelOrder(Order order);

        /// <summary>
        ///     Flag when all open orders have been received.
        /// </summary>
        void OnOpenOrdersReceived();
    }
}
