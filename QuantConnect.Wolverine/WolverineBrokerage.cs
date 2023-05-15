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

using QuantConnect.Util;
using QuantConnect.Orders;
using QuantConnect.Packets;
using QuantConnect.Interfaces;
using QuantConnect.Securities;
using QuantConnect.Brokerages;
using QuantConnect.Wolverine.Fix;
using QuantConnect.Wolverine.Fix.Core;
using QuickFix.FIX42;
using QuantConnect.Logging;
using QuantConnect.Orders.Fees;
using QuantConnect.Wolverine.Fix.Utils;
using QuantConnect.Configuration;
using QuantConnect.Api;
using System.Net.NetworkInformation;
using System.Net;
using RestSharp;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using QuickFix.Fields;

namespace QuantConnect.Wolverine
{
    [BrokerageFactory(typeof(WolverineBrokerageFactory))]
    public class WolverineBrokerage : Brokerage
    {
        private readonly IAlgorithm _algorithm;
        private readonly LiveNodePacket _job;
        private readonly IOrderProvider _orderProvider;

        private readonly ISecurityProvider _securityProvider;
        private readonly IFixBrokerageController _fixBrokerageController;
        private readonly FixInstance _fixInstance;
        private readonly WolverineSymbolMapper _symbolMapper;

        /// <summary>
        /// Returns true if we're currently connected to the broker
        /// </summary>
        public override bool IsConnected => _fixInstance.IsConnected();

        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="aggregator">consolidate ticks</param>
        public WolverineBrokerage(
            IAlgorithm algorithm, 
            LiveNodePacket job, 
            IOrderProvider orderProvider, 
            FixConfiguration fixConfiguration,
            ISecurityProvider securityProvider,
            IMapFileProvider mapFileProvider,
            bool logFixMessages) : base("Wolverine")
        {
            _job = job;
            _algorithm = algorithm;
            _securityProvider = securityProvider;
            _orderProvider = orderProvider;

            _symbolMapper = new WolverineSymbolMapper(mapFileProvider);

            _fixBrokerageController = new FixBrokerageController();
            _fixBrokerageController.ExecutionReport += OnExecutionReport;
            _fixBrokerageController.CancelReject += OnCancelReject;

            var fixProtocolDirector = new WolverineFixProtocolDirector(_symbolMapper, fixConfiguration.Account, _fixBrokerageController, _securityProvider);

            _fixInstance = new FixInstance(fixProtocolDirector, fixConfiguration, logFixMessages);
            _fixInstance.Error += (object? sender, FixError e) =>
            {
                // error event will kill the algorithm
                OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Error, -1, e.Message));
            };
            ValidateSubscription();
        }

        #region Brokerage

        /// <summary>
        /// Gets all open orders on the account.
        /// NOTE: The order objects returned do not have QC order IDs.
        /// </summary>
        /// <returns>The open orders returned from IB</returns>
        public override List<Order> GetOpenOrders()
        {
            return new List<Order>();
        }

        /// <summary>
        /// Gets all holdings for the account
        /// </summary>
        /// <returns>The current holdings from the account</returns>
        public override List<Holding> GetAccountHoldings()
        {
            return GetAccountHoldings(_job.BrokerageData, _algorithm.Securities.Values);
        }

        /// <summary>
        /// Gets the current cash balance for each currency held in the brokerage account
        /// </summary>
        /// <returns>The current cash balance for each currency available for trading</returns>
        public override List<CashAmount> GetCashBalance()
        {
            return GetCashBalance(_job.BrokerageData, _algorithm.Portfolio.CashBook);
        }

        /// <summary>
        /// Places a new order and assigns a new broker ID to the order
        /// </summary>
        /// <param name="order">The order to be placed</param>
        /// <returns>True if the request for a new order has been placed, false otherwise</returns>
        public override bool PlaceOrder(Order order)
        {
            return _fixBrokerageController.PlaceOrder(order);
        }

        /// <summary>
        /// Updates the order with the same id
        /// </summary>
        /// <param name="order">The new order information</param>
        /// <returns>True if the request was made for the order to be updated, false otherwise</returns>
        public override bool UpdateOrder(Order order)
        {
            return _fixBrokerageController.UpdateOrder(order);
        }

        /// <summary>
        /// Cancels the order with the specified ID
        /// </summary>
        /// <param name="order">The order to cancel</param>
        /// <returns>True if the request was made for the order to be canceled, false otherwise</returns>
        public override bool CancelOrder(Order order)
        {
            return _fixBrokerageController.CancelOrder(order);
        }

        /// <summary>
        /// Connects the client to the broker's remote servers
        /// </summary>
        public override void Connect()
        {
            _fixInstance.Initialize();
        }

        /// <summary>
        /// Disconnects the client from the broker's remote servers
        /// </summary>
        public override void Disconnect()
        {
            _fixInstance.Terminate();
        }

        public override void Dispose()
        {
            _fixInstance.DisposeSafely();
        }

        #endregion

        private void OnCancelReject(object? sender, OrderCancelReject rejection)
        {
            try
            {
                var orderId = rejection.IsSetField(ClOrdID.TAG) ? rejection.ClOrdID.getValue() : rejection.OrigClOrdID.getValue();
                var order = _orderProvider.GetOrdersByBrokerageId(orderId).SingleOrDefault();

                if (order == null)
                {
                    Log.Error($"WolverineBrokerage.OnCancelReject(): Unable to locate order with BrokerageId: {orderId}");
                    return;
                }

                var reason = rejection.CxlRejReason.getValue() switch
                {
                    CxlRejReason.TOO_LATE_TO_CANCEL => "Too late to cancel",
                    CxlRejReason.UNKNOWN_ORDER => "Unknown order",
                    CxlRejReason.BROKER_OPTION => "Broker option",
                    CxlRejReason.ORDER_ALREADY_IN_PENDING_CANCEL_OR_PENDING_REPLACE_STATUS =>
                        "Order already in Pending Cancel or Pending Replace status",
                    _ => string.Empty
                };

                var responseTo = rejection.CxlRejResponseTo.getValue() switch
                {
                    CxlRejResponseTo.ORDER_CANCEL_REQUEST => "Order cancel request",
                    CxlRejResponseTo.ORDER_CANCEL_REPLACE_REQUEST => "Order cancel replace request",
                    _ => string.Empty
                };

                var text = string.Empty;
                if (rejection.IsSetField(Text.TAG))
                {
                    text = rejection.Text.getValue();
                    if (!string.IsNullOrEmpty(text))
                    {
                        text = $", {text}";
                    }
                }

                var message = $"Order cancellation failed: {reason}{text}, in response to {responseTo}. OrderID: {order.Id}";
                OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Warning, 0, message));
            }
            catch (Exception e)
            {
                Log.Trace($"WolverineBrokerage.OnCancelReject(): Unexpected error {e.Message}");
            }
        }

        private void OnExecutionReport(object sender, ExecutionReport e)
        {
            OrderStatus orderStatus = Utility.ConvertOrderStatus(e);

            var orderId = orderStatus == OrderStatus.Canceled || orderStatus == OrderStatus.CancelPending || orderStatus == OrderStatus.UpdateSubmitted
                ? e.OrigClOrdID.getValue()
                : e.ClOrdID.getValue();

            DateTime time;
            try
            {
                time = e.TransactTime.getValue();
            }
            catch
            {
                // not there, happens on rejection
                time = DateTime.UtcNow;
            }

            var order = _orderProvider.GetOrdersByBrokerageId(orderId)?.SingleOrDefault();

            if (order == null)
            {
                Log.Error($"WolverineBrokerage.OnExecutionReport(): Unable to locate order with BrokerageId: {orderId}");
                return;
            }

            var message = "Wolverine Order Event";
            if (e.IsSetText())
            {
                message += $" - {e.Text.getValue()}";
            }

            var orderEvent = new OrderEvent(order, time, OrderFee.Zero, message)
            {
                Status = orderStatus
            };

            if (orderStatus == OrderStatus.Filled || orderStatus == OrderStatus.PartiallyFilled)
            {
                var filledQuantity = e.LastShares.getValue();
                var remainingQuantity = order.AbsoluteQuantity - e.CumQty.getValue();

                orderEvent.FillQuantity = filledQuantity * (order.Direction == OrderDirection.Buy ? 1 : -1);
                orderEvent.FillPrice = e.LastPx.getValue();

                if (remainingQuantity > 0)
                {
                    orderEvent.Message += " - " + remainingQuantity + " remaining";
                }
            }

            OnOrderEvent(orderEvent);
        }

        private class ModulesReadLicenseRead : Api.RestResponse
        {
            [JsonProperty(PropertyName = "license")]
            public string License;
            [JsonProperty(PropertyName = "organizationId")]
            public string OrganizationId;
        }

        /// <summary>
        /// Validate the user of this project has permission to be using it via our web API.
        /// </summary>
        private static void ValidateSubscription()
        {
            try
            {
                var productId = 221;
                var userId = Config.GetInt("job-user-id");
                var token = Config.Get("api-access-token");
                var organizationId = Config.Get("job-organization-id", null);
                // Verify we can authenticate with this user and token
                var api = new ApiConnection(userId, token);
                if (!api.Connected)
                {
                    throw new ArgumentException("Invalid api user id or token, cannot authenticate subscription.");
                }
                // Compile the information we want to send when validating
                var information = new Dictionary<string, object>()
                {
                    {"productId", productId},
                    {"machineName", Environment.MachineName},
                    {"userName", Environment.UserName},
                    {"domainName", Environment.UserDomainName},
                    {"os", Environment.OSVersion}
                };
                // IP and Mac Address Information
                try
                {
                    var interfaceDictionary = new List<Dictionary<string, object>>();
                    foreach (var nic in NetworkInterface.GetAllNetworkInterfaces().Where(nic => nic.OperationalStatus == OperationalStatus.Up))
                    {
                        var interfaceInformation = new Dictionary<string, object>();
                        // Get UnicastAddresses
                        var addresses = nic.GetIPProperties().UnicastAddresses
                            .Select(uniAddress => uniAddress.Address)
                            .Where(address => !IPAddress.IsLoopback(address)).Select(x => x.ToString());
                        // If this interface has non-loopback addresses, we will include it
                        if (!addresses.IsNullOrEmpty())
                        {
                            interfaceInformation.Add("unicastAddresses", addresses);
                            // Get MAC address
                            interfaceInformation.Add("MAC", nic.GetPhysicalAddress().ToString());
                            // Add Interface name
                            interfaceInformation.Add("name", nic.Name);
                            // Add these to our dictionary
                            interfaceDictionary.Add(interfaceInformation);
                        }
                    }
                    information.Add("networkInterfaces", interfaceDictionary);
                }
                catch (Exception)
                {
                    // NOP, not necessary to crash if fails to extract and add this information
                }
                // Include our OrganizationId is specified
                if (!string.IsNullOrEmpty(organizationId))
                {
                    information.Add("organizationId", organizationId);
                }
                var request = new RestRequest("modules/license/read", Method.POST) { RequestFormat = DataFormat.Json };
                request.AddParameter("application/json", JsonConvert.SerializeObject(information), ParameterType.RequestBody);
                api.TryRequest(request, out ModulesReadLicenseRead result);
                if (!result.Success)
                {
                    throw new InvalidOperationException($"Request for subscriptions from web failed, Response Errors : {string.Join(',', result.Errors)}");
                }

                var encryptedData = result.License;
                // Decrypt the data we received
                DateTime? expirationDate = null;
                long? stamp = null;
                bool? isValid = null;
                if (encryptedData != null)
                {
                    // Fetch the org id from the response if we are null, we need it to generate our validation key
                    if (string.IsNullOrEmpty(organizationId))
                    {
                        organizationId = result.OrganizationId;
                    }
                    // Create our combination key
                    var password = $"{token}-{organizationId}";
                    var key = SHA256.HashData(Encoding.UTF8.GetBytes(password));
                    // Split the data
                    var info = encryptedData.Split("::");
                    var buffer = Convert.FromBase64String(info[0]);
                    var iv = Convert.FromBase64String(info[1]);
                    // Decrypt our information
                    using var aes = new AesManaged();
                    var decryptor = aes.CreateDecryptor(key, iv);
                    using var memoryStream = new MemoryStream(buffer);
                    using var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
                    using var streamReader = new StreamReader(cryptoStream);
                    var decryptedData = streamReader.ReadToEnd();
                    if (!decryptedData.IsNullOrEmpty())
                    {
                        var jsonInfo = JsonConvert.DeserializeObject<JObject>(decryptedData);
                        expirationDate = jsonInfo["expiration"]?.Value<DateTime>();
                        isValid = jsonInfo["isValid"]?.Value<bool>();
                        stamp = jsonInfo["stamped"]?.Value<int>();
                    }
                }
                // Validate our conditions
                if (!expirationDate.HasValue || !isValid.HasValue || !stamp.HasValue)
                {
                    throw new InvalidOperationException("Failed to validate subscription.");
                }

                var nowUtc = DateTime.UtcNow;
                var timeSpan = nowUtc - Time.UnixTimeStampToDateTime(stamp.Value);
                if (timeSpan > TimeSpan.FromHours(12))
                {
                    throw new InvalidOperationException("Invalid API response.");
                }
                if (!isValid.Value)
                {
                    throw new ArgumentException($"Your subscription is not valid, please check your product subscriptions on our website.");
                }
                if (expirationDate < nowUtc)
                {
                    throw new ArgumentException($"Your subscription expired {expirationDate}, please renew in order to use this product.");
                }
            }
            catch (Exception e)
            {
                Log.Error($"ValidateSubscription(): Failed during validation, shutting down. Error : {e.Message}");
                Environment.Exit(1);
            }
        }
    }
}
