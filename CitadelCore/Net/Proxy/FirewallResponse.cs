/*
* Copyright © 2017-Present Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

namespace CitadelCore.Net.Proxy
{
    /// <summary>
    /// A response to a firewall inquiry for a specific traffic flow.
    /// </summary>
    public class FirewallResponse
    {
        /// <summary>
        /// The action to take.
        /// </summary>
        public FirewallAction Action
        {
            get;
            private set;
        }

        /// <summary>
        /// Optional encryption hint. This enables the client to provide a hint to the filtering
        /// engine as to whether or not some application on some non-standard port is using HTTPS
        /// encryption. If this is set to a non-null value, and the flow is on a non-standard port,
        /// then the engine will handle the flow according to this value.
        /// </summary>
        public bool? EncryptedHint
        {
            get;
            private set;
        }

        /// <summary>
        /// Constructs a new FirewallResponse with the given action.
        /// </summary>
        /// <param name="action">
        /// The action to take.
        /// </param>
        /// <param name="encryptHint">
        /// Optional encryption hint. This enables the client to provide a hint to the filtering
        /// engine as to whether or not some application on some non-standard port is using HTTPS
        /// encryption. If this is set to a non-null value, and the flow is on a non-standard port,
        /// then the engine will handle the flow according to this value.
        /// </param>
        public FirewallResponse(FirewallAction action, bool? encryptHint = null)
        {
            Action = action;
            EncryptedHint = encryptHint;
        }
    }
}