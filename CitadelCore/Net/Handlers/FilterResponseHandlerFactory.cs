/*
* Copyright © 2017-Present Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using CitadelCore.Net.Proxy;
using Microsoft.AspNetCore.Http;

namespace CitadelCore.Net.Handlers
{
    internal class FilterResponseHandlerFactory
    {
        private static readonly FilterResponseHandlerFactory s_inst = new FilterResponseHandlerFactory();

        public static FilterResponseHandlerFactory Default
        {
            get
            {
                return s_inst;
            }
        }

        public MessageBeginCallback MessageBeginCallback
        {
            get;
            set;
        }

        public MessageEndCallback MessageEndCallback
        {
            get;
            set;
        }

        public AbstractFilterResponseHandler GetHandler(HttpContext context)
        {
            if(context.WebSockets.IsWebSocketRequest)
            {
                return HandleWebsocket(context);
            }

            return HandleHttp(context);
        }

        private AbstractFilterResponseHandler HandleWebsocket(HttpContext context)
        {
            return new FilterWebsocketHandler(MessageBeginCallback, MessageEndCallback);
        }

        private AbstractFilterResponseHandler HandleHttp(HttpContext context)
        {
            return new FilterHttpResponseHandler(MessageBeginCallback, MessageEndCallback);
        }

        private AbstractFilterResponseHandler HandleUnknownProtocol(HttpContext context)
        {
            return new FilterPassthroughResponseHandler(MessageBeginCallback, MessageEndCallback);
        }
    }
}