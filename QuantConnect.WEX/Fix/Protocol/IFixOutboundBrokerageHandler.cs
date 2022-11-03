using QuantConnect.Orders;

namespace QuantConnect.WEX.Fix.Protocol
{
    public interface IFixOutboundBrokerageHandler
    {
        bool RequestOpenOrders();

        /// <summary>
        /// Places an order.
        /// </summary>
        /// <param name="order">Order to submit</param>
        /// <returns>Whether the request was sent</returns>
        bool PlaceOrder(Order order);

        /// <summary>
        /// Updates an order.
        /// </summary>
        /// <param name="order">Order to update</param>
        /// <returns>Whether the request was sent</returns>
        bool UpdateOrder(Order order);

        /// <summary>
        /// Cancels an order.
        /// </summary>
        /// <param name="order">Order to cancel</param>
        /// <returns>Whether the request was sent</returns>
        bool CancelOrder(Order order);
    }
}
