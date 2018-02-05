// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
// Adapted by CloudVeil Technology, Inc. for use in CitadelCore

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.WebSockets.Protocol;
using Microsoft.AspNetCore.WebSockets.Server;
using CitadelCore.Logging;
using CitadelCore.Extensions;

using System.Text;
using CitadelCore.Net.Http;

namespace CitadelCore.Net.WebSockets
{

    /// <summary>
    /// Not intended to replace the Microsoft websocket middleware, but rather complement it by
    /// permitting us to do some upstream negotiation before the websocket is accepted.
    /// </summary>
    public class CitadelWebSocketMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly WebSocketOptions _options;
        private IHttpWebSocketFeature _oldFeature;

        public CitadelWebSocketMiddleware(RequestDelegate next, IOptions<WebSocketOptions> options)
        {
            if (next == null)
            {
                throw new ArgumentNullException(nameof(next));
            }
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            _next = next;
            _options = options.Value;

            // TODO: validate options.
        }

        public Task Invoke(HttpContext context)
        {
            // Detect if an opaque upgrade is available. If so, add a websocket upgrade.
            var upgradeFeature = context.Features.Get<IHttpUpgradeFeature>();
            _oldFeature = context.Features.Get<IHttpWebSocketFeature>();

            if (upgradeFeature != null)
            {
                context.Features.Set<IHttpWebSocketFeature>(new CitadelHandshake(context, upgradeFeature, _options, _oldFeature));
            }

            return _next(context);
        }

        // NOTE: This is what we need to adapt for server negotiation rather than our current Filtering handler for websockets.
        private class CitadelHandshake : IHttpWebSocketFeature
        {
            private readonly HttpContext _context;
            private readonly IHttpUpgradeFeature _upgradeFeature;
            private readonly WebSocketOptions _options;
            private readonly IHttpWebSocketFeature _overridenFeature;

            public CitadelHandshake(HttpContext context, IHttpUpgradeFeature upgradeFeature, WebSocketOptions options, IHttpWebSocketFeature oldFeature)
            {
                _context = context;
                _upgradeFeature = upgradeFeature;
                _options = options;
                _overridenFeature = oldFeature;
            }

            public bool IsWebSocketRequest
            {
                get
                {
                    if (!_upgradeFeature.IsUpgradableRequest)
                    {
                        return false;
                    }
                    var headers = new List<KeyValuePair<string, string>>();
                    foreach (string headerName in HandshakeHelpers.NeededHeaders)
                    {
                        foreach (var value in _context.Request.Headers.GetCommaSeparatedValues(headerName))
                        {
                            headers.Add(new KeyValuePair<string, string>(headerName, value));
                        }
                    }
                    return HandshakeHelpers.CheckSupportedWebSocketRequest(_context.Request.Method, headers);
                }
            }

            public async Task<WebSocket> AcceptAsync(WebSocketAcceptContext acceptContext)
            {
                if (!IsWebSocketRequest)
                {
                    throw new InvalidOperationException("Not a WebSocket request."); // TODO: LOC
                }

                // First we need the URL for this connection, since it's been requested to be upgraded to
                // a websocket.
                var fullUrl = Microsoft.AspNetCore.Http.Extensions.UriHelper.GetDisplayUrl(_context.Request);

                // Need to replate the scheme with appropriate websocket scheme.
                if (fullUrl.StartsWith("http://"))
                {
                    fullUrl = "ws://" + fullUrl.Substring(7);
                }
                else if (fullUrl.StartsWith("https://"))
                {
                    fullUrl = "wss://" + fullUrl.Substring(8);
                }

                // Next we need to try and parse the URL as a URI, because the websocket client requires
                // this for connecting upstream.
                Uri wsUri = null;

                if (!Uri.TryCreate(fullUrl, UriKind.RelativeOrAbsolute, out wsUri))
                {
                    LoggerProxy.Default.Error("Failed to parse websocket URI.");
                    return null;
                }

                /*
                TimeSpan keepAliveInterval = _options.KeepAliveInterval;
                int receiveBufferSize = _options.ReceiveBufferSize;
                var advancedAcceptContext = acceptContext as ExtendedWebSocketAcceptContext;

                if (advancedAcceptContext != null)
                {
                    if (advancedAcceptContext.ReceiveBufferSize.HasValue)
                    {
                        receiveBufferSize = advancedAcceptContext.ReceiveBufferSize.Value;
                    }
                    if (advancedAcceptContext.KeepAliveInterval.HasValue)
                    {
                        keepAliveInterval = advancedAcceptContext.KeepAliveInterval.Value;
                    }
                }*/

                string subProtocol = _context.Request.Headers[Constants.Headers.SecWebSocketProtocol];

                // TODO: Rip server negotiation code from FilterWebsocketHandler and put it here.
                var wsServer = new System.Net.WebSockets.Managed.ClientWebSocket();

                if(subProtocol != null && subProtocol.Length > 0)
                {
                    wsServer.Options.AddSubProtocol(subProtocol);
                }

                wsServer.Options.Cookies = new System.Net.CookieContainer();

                foreach (var cookie in _context.Request.Cookies)
                {
                    try
                    {
                        wsServer.Options.Cookies.Add(new Uri(fullUrl, UriKind.Absolute), new System.Net.Cookie(cookie.Key, System.Net.WebUtility.UrlEncode(cookie.Value)));
                    }
                    catch (Exception e)
                    {
                        LoggerProxy.Default.Error("Error while attempting to add websocket cookie.");
                        LoggerProxy.Default.Error(e);
                    }
                }

                if (_context.Connection.ClientCertificate != null)
                {
                    wsServer.Options.ClientCertificates = new System.Security.Cryptography.X509Certificates.X509CertificateCollection(new[] { _context.Connection.ClientCertificate.ToV2Certificate() });
                }

                LoggerProxy.Default.Info(string.Format("Connecting websocket to {0}", wsUri.AbsoluteUri));

                var reqHeaderBuilder = new StringBuilder();
                foreach (var hdr in _context.Request.Headers)
                {
                    if (!ForbiddenHttpHeaders.IsForbidden(hdr.Key))
                    {
                        reqHeaderBuilder.AppendFormat("{0}: {1}\r\n", hdr.Key, hdr.Value.ToString());

                        try
                        {
                            if (!ForbiddenWsHeaders.IsForbidden(hdr.Key))
                            {
                                wsServer.Options.SetRequestHeader(hdr.Key, hdr.Value.ToString());
                                Console.WriteLine("Set Header: {0} ::: {1}", hdr.Key, hdr.Value.ToString());
                            }
                        }
                        catch (Exception hdrException)
                        {
                            Console.WriteLine("Failed Header: {0} ::: {1}", hdr.Key, hdr.Value.ToString());
                            LoggerProxy.Default.Error(hdrException);
                        }
                    }
                }

                string serverSubProtocol = null;

                await wsServer.ConnectAsync(wsUri, _context.RequestAborted);
                if (wsServer.State == WebSocketState.Open)
                {
                    serverSubProtocol = wsServer.SubProtocol;
                }
                else
                {
                    
                }

                // FIXME: We need to check here for socket closed by the server and abort the client one accordingly?

                WebSocket clientWebSocket = await _overridenFeature.AcceptAsync(new WebSocketAcceptContext() { SubProtocol = serverSubProtocol }); // Pass the negotiated sub protocol back to the client browser.

                if (clientWebSocket != null)
                {
                    LoggerProxy.Default.Info($"Successfully negotiated client socket with sub protocol of {subProtocol}. Server sub protocol negotiated was {serverSubProtocol}");

                    // Add the negotiated pair to our websocket manager so that we can return control back to the FilterWebSocketHandler.Handle for filtering.
                    CitadelWebSocketManager.Default.AddNegotiatedSocketPair(clientWebSocket, new WebSocketInfo()
                    {
                        ClientSocket = wsServer,
                        RequestHeaders = reqHeaderBuilder.ToString()
                    }, _context.RequestAborted);
                }

                return clientWebSocket;

                /*string key = string.Join(", ", _context.Request.Headers[Constants.Headers.SecWebSocketKey]);

                var responseHeaders = HandshakeHelpers.GenerateResponseHeaders(key, subProtocol);
                foreach (var headerPair in responseHeaders)
                {
                    _context.Response.Headers[headerPair.Key] = headerPair.Value;
                }

                Stream opaqueTransport = await _upgradeFeature.UpgradeAsync(); // Sets status code to 101

                return WebSocketProtocol.CreateFromStream(opaqueTransport, isServer: true, subProtocol: subProtocol, keepAliveInterval: keepAliveInterval);*/
            }
        }
    }
}
