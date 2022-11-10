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

using QuickFix;

namespace QuantConnect.WEX.Fix.Protocol
{
    /// <summary>
    ///     Applies protocol specific customizations, and helps direct FIX messages to specific handlers.
    /// </summary>
    public interface IFixProtocolDirector
    {
        /// <summary>
        ///     Returns true if all sessions are logged in and ready to accept order routing requests.
        /// </summary>
        bool AreSessionsReady();

        /// <summary>
        ///     Defines the FIX protocol being used.
        /// </summary>
        IMessageFactory MessageFactory { get; }

        /// <summary>
        ///     Pass a message to the director to be handled.
        /// </summary>
        /// <param name="msg">Message to process</param>
        /// <param name="sessionId">Session the message is from</param>
        void Handle(Message msg, SessionID sessionId);

        /// <summary>
        ///     Pass an admin message to the director to be handled.
        /// </summary>
        /// <param name="msg">Message to process</param>
        void HandleAdminMessage(Message msg);

        /// <summary>
        ///     Allow for enrichment / customization of any outgoing messages (such as logon).
        /// </summary>
        /// <param name="msg">Message to customize.</param>
        void EnrichOutbound(Message msg);

        /// <summary>
        ///     Called when a session logs on.
        /// </summary>
        /// <param name="sessionId"></param>
        void OnLogon(SessionID sessionId);

        /// <summary>
        ///     Called when a session logs out.
        /// </summary>
        /// <param name="sessionId"></param>
        void OnLogout(SessionID sessionId);
    }
}