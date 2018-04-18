/*
* Copyright © 2017 Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System.Net.WebSockets.Managed;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;
using CitadelCore.Net.Proxy;
using CitadelCore.Logging;
using System.Text;
using System.Threading;
using CitadelCore.Net.Http;
using CitadelCore.Extensions;
using System.Collections.Generic;

namespace CitadelCore.Net.Handlers
{

    /// <summary>
    /// The FilterWebsocketHandler handler is designed to proxy Websocket requests and responses,
    /// while providing an opportunity for users to inspect and optionally filter and modifiy
    /// requests and responses at different stages of the transaction.
    /// </summary>
    internal class FilterWebsocketHandler : AbstractFilterResponseHandler
    {
        public FilterWebsocketHandler(MessageBeginCallback messageBeginCallback, MessageEndCallback messageEndCallback) : base(messageBeginCallback, messageEndCallback)
        {

        }

        public override async Task Handle(HttpContext context)
        {
            try
            {
                // First we need the URL for this connection, since it's been requested to be upgraded to
                // a websocket.
                var fullUrl = Microsoft.AspNetCore.Http.Extensions.UriHelper.GetDisplayUrl(context.Request);

                // Need to replate the scheme with appropriate websocket scheme.
                if(fullUrl.StartsWith("http://"))
                {
                    fullUrl = "ws://" + fullUrl.Substring(7);
                }
                else if(fullUrl.StartsWith("https://"))
                {
                    fullUrl = "wss://" + fullUrl.Substring(8);
                }

                // Next we need to try and parse the URL as a URI, because the websocket client requires
                // this for connecting upstream.
                Uri wsUri = null;

                if(!Uri.TryCreate(fullUrl, UriKind.RelativeOrAbsolute, out wsUri))
                {
                    LoggerProxy.Default.Error("Failed to parse websocket URI.");
                    return;
                }

                // Create the websocket that's going to connect to the remote server.
                ClientWebSocket wsServer = new ClientWebSocket();
                wsServer.Options.Cookies = new System.Net.CookieContainer();
                wsServer.Options.SetBuffer((int)ushort.MaxValue * 16, (int)ushort.MaxValue * 16);

                foreach (var cookie in context.Request.Cookies)
                {
                    try
                    {
                        wsServer.Options.Cookies.Add(new Uri(fullUrl, UriKind.Absolute), new System.Net.Cookie(cookie.Key, System.Net.WebUtility.UrlEncode(cookie.Value)));                        
                    }
                    catch(Exception e)
                    {
                        LoggerProxy.Default.Error("Error while attempting to add websocket cookie.");
                        LoggerProxy.Default.Error(e);                        
                    }
                }

                if(context.Connection.ClientCertificate != null)
                {
                    wsServer.Options.ClientCertificates = new System.Security.Cryptography.X509Certificates.X509CertificateCollection(new[] { context.Connection.ClientCertificate.ToV2Certificate() });
                }

                var reqHeaderBuilder = new StringBuilder();
                foreach(var hdr in context.Request.Headers)
                {
                    if(!ForbiddenHttpHeaders.IsForbidden(hdr.Key))
                    {
                        reqHeaderBuilder.AppendFormat("{0}: {1}\r\n", hdr.Key, hdr.Value.ToString());

                        try
                        {
                            if(!ForbiddenWsHeaders.IsForbidden(hdr.Key))
                            {
                                wsServer.Options.SetRequestHeader(hdr.Key, hdr.Value.ToString());
                                Console.WriteLine("Set Header: {0} ::: {1}", hdr.Key, hdr.Value.ToString());
                            }
                        }
                        catch(Exception hdrException)
                        {
                            Console.WriteLine("Failed Header: {0} ::: {1}", hdr.Key, hdr.Value.ToString());
                            LoggerProxy.Default.Error(hdrException);
                        }
                    }
                }

                reqHeaderBuilder.Append("\r\n");

                LoggerProxy.Default.Info(string.Format("Connecting websocket to {0}", wsUri.AbsoluteUri));

                // Connect the server websocket to the upstream, remote webserver.
                await wsServer.ConnectAsync(wsUri, context.RequestAborted);
                
                LoggerProxy.Default.Info(String.Format("Connected websocket to {0}", wsUri.AbsoluteUri));

                // Create, via acceptor, the client websocket. This is the local machine's websocket.
                var wsClient = await context.WebSockets.AcceptWebSocketAsync(wsServer.SubProtocol ?? null);

                ProxyNextAction nxtAction = ProxyNextAction.AllowAndIgnoreContentAndResponse;
                string customResponseContentType = string.Empty;
                byte[] customResponse = null;
                m_msgBeginCb?.Invoke(wsUri, reqHeaderBuilder.ToString(), null, context.Request.IsHttps ? MessageType.SecureWebSocket : MessageType.WebSocket, MessageDirection.Request, out nxtAction, out customResponseContentType, out customResponse);

                switch(nxtAction)
                {
                    case ProxyNextAction.DropConnection:
                        {
                            if(customResponse != null)
                            {

                            }

                            await wsClient.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                            return;
                        }
                }

                // Spawn an async task that will poll the remote server for data in a loop, and then
                // write any data it gets to the client websocket.
                var serverTask = Task.Run(async () =>
                {
                    System.Net.WebSockets.WebSocketReceiveResult serverStatus = null;
                    var serverBuffer = new byte[1024 * 4];
                    try
                    {
                        bool looping = true;

                        serverStatus = await wsServer.ReceiveAsync(new ArraySegment<byte>(serverBuffer), context.RequestAborted);

                        while (looping && !serverStatus.CloseStatus.HasValue && !context.RequestAborted.IsCancellationRequested)
                        {
                            await wsClient.SendAsync(new ArraySegment<byte>(serverBuffer, 0, serverStatus.Count), serverStatus.MessageType, serverStatus.EndOfMessage, context.RequestAborted);

                            if(!wsClient.CloseStatus.HasValue)
                            {
                                serverStatus = await wsServer.ReceiveAsync(new ArraySegment<byte>(serverBuffer), context.RequestAborted);
                                continue;
                            }

                            looping = false;
                        }

                        await wsClient.CloseAsync(serverStatus.CloseStatus.Value, serverStatus.CloseStatusDescription, context.RequestAborted);
                    }
                    catch
                    {
                        try
                        {
                            var closeStatus = serverStatus?.CloseStatus ?? System.Net.WebSockets.WebSocketCloseStatus.NormalClosure;
                            var closeMessage = serverStatus?.CloseStatusDescription ?? string.Empty;

                            await wsClient.CloseAsync(closeStatus, closeMessage, context.RequestAborted);
                        }
                        catch { }
                    }
                });

                // Spawn an async task that will poll the local client websocket, in a loop, and then
                // write any data it gets to the remote server websocket.
                var clientTask = Task.Run(async () =>
                {
                    System.Net.WebSockets.WebSocketReceiveResult clientResult = null;
                    var clientBuffer = new byte[1024 * 4];
                    try
                    {

                        bool looping = true;

                        clientResult = await wsClient.ReceiveAsync(new ArraySegment<byte>(clientBuffer), context.RequestAborted);

                        while (looping && !clientResult.CloseStatus.HasValue && !context.RequestAborted.IsCancellationRequested)
                        {
                            await wsServer.SendAsync(new ArraySegment<byte>(clientBuffer, 0, clientResult.Count), clientResult.MessageType, clientResult.EndOfMessage, context.RequestAborted);

                            if(!wsServer.CloseStatus.HasValue)
                            {
                                clientResult = await wsClient.ReceiveAsync(new ArraySegment<byte>(clientBuffer), context.RequestAborted);
                                continue;
                            }

                            looping = false;
                        }
                        
                        await wsServer.CloseAsync(clientResult.CloseStatus.Value, clientResult.CloseStatusDescription, context.RequestAborted);
                    }
                    catch
                    {
                        try
                        {
                            var closeStatus = clientResult?.CloseStatus ?? System.Net.WebSockets.WebSocketCloseStatus.NormalClosure;
                            var closeMessage = clientResult?.CloseStatusDescription ?? string.Empty;

                            await wsServer.CloseAsync(closeStatus, closeMessage, context.RequestAborted);
                        }
                        catch { }
                    }
                });

                // Above, we have created a bridge between the local and remote websocket. Wait for both
                // associated tasks to complete.
                await Task.WhenAll(serverTask, clientTask);
            }
            catch(Exception wshe)
            {
                if(wshe is System.Net.WebSockets.WebSocketException)
                {
                    var cast = wshe as System.Net.WebSockets.WebSocketException;

                    Console.WriteLine(cast.WebSocketErrorCode);
                    if(cast.Data != null)
                    {
                        foreach(KeyValuePair<object, object> kvp in cast.Data)
                        {
                            Console.WriteLine("{0} ::: {1}", kvp.Key, kvp.Value);
                        }
                    }
                }
                LoggerProxy.Default.Error(wshe);
            }
        }
    }
}