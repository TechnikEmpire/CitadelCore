/*
* Copyright © 2018-Present Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

namespace CitadelCore.IO
{
    /// <summary>
    /// Represents the type of message in inspection callbacks.
    /// </summary>
    public enum MessageProtocol
    {
        /// <summary>
        /// The message is a HTTP message.
        /// </summary>
        Http,

        /// <summary>
        /// The message is a Websocket message.
        /// </summary>
        WebSocket
    }
}