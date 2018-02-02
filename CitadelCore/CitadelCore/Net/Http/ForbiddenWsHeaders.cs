﻿/*
* Copyright © 2018 Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System;
using System.Collections.Generic;
using System.Text;

namespace CitadelCore.Net.Http
{
    internal static class ForbiddenWsHeaders
    {
        // More headers specific to websockets here:
        // https://tools.ietf.org/html/rfc6455#section-11.3

        /// <summary>
        /// List of headers that cause headaches for proxies like me. 
        /// </summary>
        private static readonly HashSet<string> s_forbidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Our client websocket might not be the same as our server websocket. In 
            // .NET Standard 2.0 for example, the Kestrel client websocket is a completely
            // different class than the ClientWebSocket class.
            "Sec-WebSocket-Extensions",

            // We don't want to pass negotiation stuff upstream.
            "Sec-WebSocket-Key",

            // We manually add cookies so we don't want them this way.
            "Cookie",

            "Upgrade",
            "Sec-WebSocket-Version"
        };

        /// <summary>
        /// Whether or not the given header name is a forbidden header. 
        /// </summary>
        /// <param name="headerName">
        /// The header name. 
        /// </param>
        /// <returns>
        /// True if the header named is forbidden, false otherwise. 
        /// </returns>
        public static bool IsForbidden(string headerName)
        {
            if(headerName == null)
            {
                return true;
            }

            return s_forbidden.Contains(headerName);
        }
    }
}
