/*
* Copyright © 2018-Present Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

namespace CitadelCore.IO
{
    /// <summary>
    /// Represents the direction of the message. That is to say, informs as to whether or not the
    /// message is a request or response message.
    /// </summary>
    public enum MessageType
    {
        /// <summary>
        /// The message is a request message, aka originating from the local client.
        /// </summary>
        Request,

        /// <summary>
        /// The message is a response message, aka originating from the remote server.
        /// </summary>
        Response,
    }
}