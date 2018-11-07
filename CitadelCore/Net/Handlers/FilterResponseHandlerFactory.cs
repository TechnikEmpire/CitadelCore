/*
* Copyright © 2017-Present Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using CitadelCore.Net.Handlers.Replay;
using CitadelCore.Net.Proxy;
using Microsoft.AspNetCore.Http;
using System;
using System.Net;
using System.Net.Http;

namespace CitadelCore.Net.Handlers
{
    /// <summary>
    /// The FilterResponseHandlerFactory returns specialized connection handlers.
    /// </summary>
    internal class FilterResponseHandlerFactory
    {
        static FilterResponseHandlerFactory()
        {
            // Enforce global use of good/strong TLS protocols.
            ServicePointManager.SecurityProtocol = (ServicePointManager.SecurityProtocol & ~SecurityProtocolType.Ssl3) | (SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12);

            // If this isn't set, we'll have a massive bottlenet on our upstream flow. The
            // performance gains here extreme. This must be set.
            ServicePointManager.DefaultConnectionLimit = ushort.MaxValue;
            
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.CheckCertificateRevocationList = true;
            ServicePointManager.ReusePort = true;
            ServicePointManager.UseNagleAlgorithm = false;
        }

        /// <summary>
        /// Shared HttpClient instance.
        /// </summary>
        private readonly HttpClient _client;

        /// <summary>
        /// Shared, non-owning instance of the replay factory we'll let our HTTP handlers create
        /// replays with, if requested by the user.
        /// </summary>
        private readonly ReplayResponseHandlerFactory _replayFactory;

        /// <summary>
        /// Shared configuration used to construct the host proxy-server.
        /// </summary>
        private readonly ProxyServerConfiguration _configuration;

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="configuration">
        /// The shared, proxy configuration.
        /// </param>
        /// <param name="replayFactory">
        /// A shared, non-owning instance of the replay factory we'll let our HTTP handlers create
        /// replays with, if requested by the user.
        /// </param>
        internal FilterResponseHandlerFactory(ProxyServerConfiguration configuration, ReplayResponseHandlerFactory replayFactory)
        {
            _configuration = configuration;

            _replayFactory = replayFactory;

            if (replayFactory == null)
            {
                throw new ArgumentException("The replay factor must be defined.", nameof(replayFactory));
            }

            // We need UseCookies set to false here. We then need to set per-request cookies by
            // manually adding the "Cookie" header. If we don't have UseCookies set to false here,
            // this will not work.
            //
            // Of course, if the user wants to manage this, then we just use their handler.
            if (configuration.CustomProxyHandler == null)
            {
                configuration.CustomProxyHandler = new HttpClientHandler
                {

                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                    UseCookies = false,
                    ClientCertificateOptions = ClientCertificateOption.Automatic,
                    AllowAutoRedirect = false,
                    Proxy = null
                };
            }

            _client = new HttpClient(configuration.CustomProxyHandler, true);
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
            return new FilterWebsocketHandler(_configuration);
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
            return new FilterHttpResponseHandler(_client, _replayFactory, _configuration);
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
            return new FilterPassthroughResponseHandler(_configuration);
        }
    }
}