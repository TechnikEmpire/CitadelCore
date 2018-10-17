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
    /// <summary>
    /// The FilterResponseHandlerFactory returns specialized connection handlers.
    /// </summary>
    internal class FilterResponseHandlerFactory
    {
        /// <summary>
        /// The default factory.
        /// </summary>
        public static FilterResponseHandlerFactory Default
        {
            get;
        } = new FilterResponseHandlerFactory();

        /// <summary>
        /// Private constructor to enforce singleton.
        /// </summary>
        private FilterResponseHandlerFactory()
        {
        }

        /// <summary>
        /// The new message callback, supplied to newly created handlers.
        /// </summary>
        public NewHttpMessageHandler NewMessageCallback
        {
            get;
            set;
        }

        /// <summary>
        /// The whole-body content inspection callback, supplied to newly created handlers.
        /// </summary>
        public HttpMessageWholeBodyInspectionHandler WholeBodyInspectionCallback
        {
            get;
            set;
        }

        /// <summary>
        /// The streamed content inspection callback, supplied to newly created handlers.
        /// </summary>
        public HttpMessageStreamedInspectionHandler StreamedInspectionCallback
        {
            get;
            set;
        }

        /// <summary>
        /// The replay content inspection callback, supplied to newly created handlers.
        /// </summary>
        public HttpMessageReplayInspectionHandler ReplayInspectionCallback
        {
            get;
            set;
        }

        /// <summary>
        /// Constructs and returns the appropriate handler for the supplied HTTP context.
        /// </summary>
        /// <param name="context">
        /// The HTTP content.
        /// </param>
        /// <returns>
        /// The new, specialized handler for the given context.
        /// </returns>
        public AbstractFilterResponseHandler GetHandler(HttpContext context)
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                return HandleWebsocket(context);
            }

            return HandleHttp(context);
        }

        /// <summary>
        /// Constructs a new handler specially for websocket contexts.
        /// </summary>
        /// <param name="context">
        /// The HTTP context.
        /// </param>
        /// <returns>
        /// The new, specialized handler for the given context.
        /// </returns>
        private AbstractFilterResponseHandler HandleWebsocket(HttpContext context)
        {
            return new FilterWebsocketHandler(NewMessageCallback, WholeBodyInspectionCallback, StreamedInspectionCallback, ReplayInspectionCallback);
        }

        /// <summary>
        /// Constructs a new handler specially for HTTP/S contexts.
        /// </summary>
        /// <param name="context">
        /// The HTTP context.
        /// </param>
        /// <returns>
        /// The new, specialized handler for the given context.
        /// </returns>
        private AbstractFilterResponseHandler HandleHttp(HttpContext context)
        {
            return new FilterHttpResponseHandler(NewMessageCallback, WholeBodyInspectionCallback, StreamedInspectionCallback, ReplayInspectionCallback);
        }

        /// <summary>
        /// Constructs a new handler specially for unknown protocol contexts.
        /// </summary>
        /// <param name="context">
        /// The HTTP context.
        /// </param>
        /// <returns>
        /// Destroys the whole universe. This handler is not implemented so it throws an exception.
        /// Not used.
        /// </returns>
        private AbstractFilterResponseHandler HandleUnknownProtocol(HttpContext context)
        {
            return new FilterPassthroughResponseHandler(NewMessageCallback, WholeBodyInspectionCallback, StreamedInspectionCallback, ReplayInspectionCallback);
        }
    }
}