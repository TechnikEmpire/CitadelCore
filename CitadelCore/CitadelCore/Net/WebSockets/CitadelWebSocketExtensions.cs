/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/
using Microsoft.AspNetCore.Builder;
using System;
using System.Collections.Generic;
using System.Text;

namespace CitadelCore.Net.WebSockets
{
    public static class CitadelWebSocketExtensions
    {
        /// <summary>
        /// Use support for websocket protocol "pass-back".
        /// 
        /// This middleware passes the protocol that the client requests back to the client.
        /// This is faulty because we need to get the protocol, negotiate with the server, and then finish accepting the client.
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public static IApplicationBuilder UseCitadelWebSocketMiddleware(this IApplicationBuilder app)
        {
            return app.UseMiddleware(typeof(CitadelWebSocketMiddleware));
        }
    }
}
