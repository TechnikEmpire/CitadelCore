/*
* Copyright © 2017-Present Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

namespace CitadelCore.Net.Proxy
{
    /// <summary>
    /// The ProxyNextAction enum defines actions available to external users during the processing of
    /// proxied connections.
    /// </summary>
    public enum ProxyNextAction
    {
        /// <summary>
        /// Lets the connection pass through without filtering at its current state. 
        /// </summary>
        AllowAndIgnoreContent = 0,

        /// <summary>
        /// Allows the connection to proceed, but attempts to buffer the entire connection contents,
        /// which are to be passed back for inspection and further filtering.
        /// </summary>
        AllowButRequestContentInspection = 1,

        /// <summary>
        /// Immediately drops the connection with the supplied data. If no data supplied, a generic
        /// 204 No-Content will be sent to close the connection gracefully in the case of Http
        /// transactions. When the transaction is a websocket, the connection will simply be closed.
        /// </summary>
        DropConnection = 2,

        /// <summary>
        /// Allows the entire connection, including any response, to pass without any inspection or filtering. 
        /// </summary>
        AllowAndIgnoreContentAndResponse = 3
    }
}