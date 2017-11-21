/*
* Copyright © 2017 Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using CitadelCore.Logging;
using CitadelCore.Net.Http;
using CitadelCore.Net.Proxy;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CitadelCore.Net.Handlers
{
    /// <summary>
    /// The FilterHttpResponse handler is designed to proxy HTTP requests and responses, while
    /// providing an opportunity for users to inspect and optionally filter and modifiy requests and
    /// responses at different stages of the transaction.
    /// </summary>
    internal class FilterHttpResponseHandler : AbstractFilterResponseHandler
    {
        private static readonly DateTime s_Epoch = new DateTime(1970, 1, 1);

        private static readonly string s_EpochHttpDateTime = s_Epoch.ToString("r");

        private static HttpClient s_client;

        private static readonly Regex s_httpVerRegex = new Regex("([0-9]+\\.[0-9]+)", RegexOptions.Compiled | RegexOptions.ECMAScript);

        static FilterHttpResponseHandler()
        {
            // Enforce global use of good/strong TLS protocols.
            ServicePointManager.SecurityProtocol = (ServicePointManager.SecurityProtocol & ~SecurityProtocolType.Ssl3) | (SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12);

            ServicePointManager.Expect100Continue = false;
            ServicePointManager.CheckCertificateRevocationList = true;
            ServicePointManager.ReusePort = true;
            ServicePointManager.UseNagleAlgorithm = false;

            // We need UseCookies set to false here. We then need to set per-request cookies by
            // manually adding the "Cookie" header. If we don't have UseCookies set to false here,
            // this will not work.
            HttpClientHandler handler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                UseCookies = false,
                //PreAuthenticate = false,
                //UseDefaultCredentials = false,
                AllowAutoRedirect = false,                
                Proxy = null
            };

            s_client = new HttpClient(handler);
        }        

        public FilterHttpResponseHandler(MessageBeginCallback messageBeginCallback, MessageEndCallback messageEndCallback) : base(messageBeginCallback, messageEndCallback)
        {
        }

        public override async Task Handle(HttpContext context)
        {
            try
            {
                // Use helper to get the full, proper URL for the request.
                //var fullUrl = Microsoft.AspNetCore.Http.Extensions.UriHelper.GetDisplayUrl(context.Request);
                var fullUrl = Microsoft.AspNetCore.Http.Extensions.UriHelper.GetEncodedUrl(context.Request);

                // Next we need to try and parse the URL as a URI, because the websocket client
                // requires this for connecting upstream.
                Uri reqUrl = null;

                if(!Uri.TryCreate(fullUrl, UriKind.RelativeOrAbsolute, out reqUrl))
                {
                    LoggerProxy.Default.Error("Failed to parse HTTP URL.");
                    return;
                }

                // Create a new request to send out upstream.                
                var requestMsg = new HttpRequestMessage(new HttpMethod(context.Request.Method), fullUrl);
                
                if(context.Connection.ClientCertificate != null)
                {
                    // TODO - Handle client certificates.
                }

                // Build request headers into this, so we can pass the result to message begin/end callbacks.
                var reqHeaderBuilder = new StringBuilder();

                var failedInitialHeaders = new List<Tuple<string, string>>();

                bool requestHasZeroContentLength = false;

                // Clone headers from the real client request to our upstream HTTP request.
                foreach(var hdr in context.Request.Headers)
                {
                    try
                    {
                        if(hdr.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) && hdr.Value.ToString().Equals("0"))
                        {
                            requestHasZeroContentLength = true;
                        }
                    }
                    catch { }

                    try
                    {
                        reqHeaderBuilder.AppendFormat("{0}: {1}\r\n", hdr.Key, hdr.Value.ToString());
                    }
                    catch { }

                    if(ForbiddenHttpHeaders.IsForbidden(hdr.Key))
                    {
                        continue;
                    }

                    if(!requestMsg.Headers.TryAddWithoutValidation(hdr.Key, hdr.Value.ToString()))
                    {

                        string hName = hdr.Key != null ? hdr.Key : string.Empty;
                        string hValue = hdr.Value.ToString() != null ? hdr.Value.ToString() : string.Empty;
                        
                        if(hName.Length > 0 && hValue.Length > 0)
                        {
                            failedInitialHeaders.Add(new Tuple<string, string>(hName, hValue));
                        }
                    }
                }

                // Match the HTTP version of the client on the upstream request.
                // We don't want to transparently pass around headers that are wrong
                // for the client's HTTP version.
                Version upstreamReqVersionMatch = null;
                
                Match match = s_httpVerRegex.Match(context.Request.Protocol);
                if(match != null && match.Success)
                {   
                    upstreamReqVersionMatch = Version.Parse(match.Value);                    
                    requestMsg.Version = upstreamReqVersionMatch;
                }

                // Add trailing CRLF to the request headers string.
                reqHeaderBuilder.Append("\r\n");
                
                // Since headers are complete at this stage, let's do our first call to message begin
                // for the request side.
                ProxyNextAction requestNextAction = ProxyNextAction.AllowAndIgnoreContentAndResponse;
                string requestBlockResponseContentType = string.Empty;
                byte[] requestBlockResponse = null;
                m_msgBeginCb?.Invoke(reqUrl, reqHeaderBuilder.ToString(), m_nullBody, context.Request.IsHttps ? MessageType.Https : MessageType.Http, MessageDirection.Request, out requestNextAction, out requestBlockResponseContentType, out requestBlockResponse);

                if(requestNextAction == ProxyNextAction.DropConnection)
                {
                    if(requestBlockResponse != null)
                    {
                        // User wants to block this request with a custom response.
                        await DoCustomResponse(context, requestBlockResponseContentType, requestBlockResponse);
                    }
                    else
                    {
                        // User wants to block this request with a generic 204 response.
                        Do204(context);                        
                    }

                    return;
                }

                // Get the request body into memory.
                using(var ms = new MemoryStream())
                {
                    await context.Request.Body.CopyToAsync(ms);
                    //await Microsoft.AspNetCore.Http.Extensions.StreamCopyOperation.CopyToAsync(context.Request.Body, ms, null, context.RequestAborted);

                    var requestBody = ms.ToArray();

                    // If we don't have a body, there's no sense in calling the message end callback.
                    if(requestBody.Length > 0)
                    {
                        // We have a body and the user previously instructed us to give them the
                        // content, if any, for inspection.
                        if(requestNextAction == ProxyNextAction.AllowButRequestContentInspection)
                        {
                            // We'll now call the message end function for the request side.
                            bool shouldBlockRequest = false;
                            requestBlockResponseContentType = string.Empty;
                            requestBlockResponse = null;
                            m_msgEndCb?.Invoke(reqUrl, reqHeaderBuilder.ToString(), requestBody, context.Request.IsHttps ? MessageType.Https : MessageType.Http, MessageDirection.Request, out shouldBlockRequest, out requestBlockResponseContentType, out requestBlockResponse);

                            if(shouldBlockRequest)
                            {
                                // User wants to block this request after inspecting the content.

                                if(requestBlockResponse != null)
                                {
                                    // User wants to block this request with a custom response.
                                    await DoCustomResponse(context, requestBlockResponseContentType, requestBlockResponse);
                                }
                                else
                                {
                                    // User wants to block this request with a generic 204 response.
                                    Do204(context);
                                }

                                return;
                            }
                        }

                        // Set our content, even if it's empty. Don't worry about ByteArrayContent
                        // and friends setting other headers, we're gonna blow relevant headers away
                        // below and then set them properly.
                        requestMsg.Content = new ByteArrayContent(requestBody);

                        requestMsg.Content.Headers.Clear();

                        requestMsg.Content.Headers.TryAddWithoutValidation("Content-Length", requestBody.Length.ToString());
                    }
                    else
                    {
                        if(requestHasZeroContentLength)
                        {
                            requestMsg.Content = new ByteArrayContent(requestBody);
                            requestMsg.Content.Headers.Clear();
                            requestMsg.Content.Headers.TryAddWithoutValidation("Content-Length", "0");
                        }
                    }
                }

                // Ensure that content type is set properly because ByteArrayContent and friends will
                // modify these fields.
                foreach(var et in failedInitialHeaders)
                {
                    if(!requestMsg.Headers.TryAddWithoutValidation(et.Item1, et.Item2))
                    {
                        if(requestMsg.Content != null)
                        {
                            if(!requestMsg.Content.Headers.TryAddWithoutValidation(et.Item1, et.Item2))
                            {
                                LoggerProxy.Default.Warn(string.Format("Failed to add HTTP header with key {0} and with value {1}.", et.Item1, et.Item2));
                            }
                        }
                    }
                }                

                // Lets start sending the request upstream. We're going to as the client to return
                // control to us when the headers are complete. This way we're not buffering entire
                // responses into memory, and if the user doesn't request to inspect the content, we
                // can just async stream the content transparently and Kestrel is so cool and sweet
                // and nice, it'll automatically stream as chunked content.
                HttpResponseMessage response = null;

                try
                {   
                    response = await s_client.SendAsync(requestMsg, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
                }
                catch(Exception e)
                {
                    LoggerProxy.Default.Error(e);
                }

                if(response == null)
                {
                    return;
                }

                // Blow away all response headers. We wanna clone these now from our upstream request.
                context.Response.Headers.Clear();

                // Ensure our client's response status code is set to match ours.
                context.Response.StatusCode = (int)response.StatusCode;

                // Build response headers into this, so we can pass the result to message begin/end callbacks.
                var resHeaderBuilder = new StringBuilder();

                bool responseHasZeroContentLength = false;

                // Iterate over all upstream response headers. Note that response.Content.Headers is
                // not ALL headers. Headers are split up into different properties according to
                // logical grouping.
                foreach(var hdr in response.Content.Headers)
                {
                    try
                    {
                        if(hdr.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) && hdr.Value.ToString().Equals("0"))
                        {
                            responseHasZeroContentLength = true;
                        }
                    }
                    catch { }

                    try
                    {
                        resHeaderBuilder.AppendFormat("{0}: {1}\r\n", hdr.Key, string.Join(", ", hdr.Value));
                    }
                    catch { }

                    if(ForbiddenHttpHeaders.IsForbidden(hdr.Key))
                    {
                        continue;
                    }

                    try
                    {   
                        context.Response.Headers.Add(hdr.Key, new Microsoft.Extensions.Primitives.StringValues(hdr.Value.ToArray()));
                    }
                    catch(Exception e)
                    {
                        LoggerProxy.Default.Error(e);
                    }
                }

                // As mentioned above, headers are split up into different properties. We need to now
                // clone over the generic headers.
                foreach(var hdr in response.Headers)
                {
                    try
                    {
                        if(hdr.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) && hdr.Value.ToString().Equals("0"))
                        {
                            responseHasZeroContentLength = true;
                        }
                    }
                    catch { }

                    try
                    {
                        resHeaderBuilder.AppendFormat("{0}: {1}\r\n", hdr.Key, string.Join(", ", hdr.Value));
                    }
                    catch { }

                    if(ForbiddenHttpHeaders.IsForbidden(hdr.Key))
                    {
                        continue;
                    }

                    try
                    {
                        context.Response.Headers.Add(hdr.Key, new Microsoft.Extensions.Primitives.StringValues(hdr.Value.ToArray()));
                    }
                    catch(Exception e)
                    {
                        LoggerProxy.Default.Error(e);
                    }
                }

                resHeaderBuilder.Append("\r\n");

                // Now that we have response headers, let's call the message begin handler for the
                // response. Unless of course, the user has asked us NOT to do this.
                if(requestNextAction != ProxyNextAction.AllowAndIgnoreContentAndResponse)
                {
                    ProxyNextAction responseNextAction = ProxyNextAction.AllowAndIgnoreContent;
                    string responseBlockResponseContentType = string.Empty;
                    byte[] responseBlockResponse = null;

                    m_msgBeginCb?.Invoke(reqUrl, resHeaderBuilder.ToString(), m_nullBody, context.Request.IsHttps ? MessageType.Https : MessageType.Http, MessageDirection.Response, out responseNextAction, out responseBlockResponseContentType, out responseBlockResponse);

                    if(responseNextAction == ProxyNextAction.DropConnection)
                    {
                        if(responseBlockResponse != null)
                        {
                            // User wants to block this response with a custom response.
                            await DoCustomResponse(context, responseBlockResponseContentType, responseBlockResponse);                            
                        }
                        else
                        {
                            // User wants to block this response with a generic 204 response.
                            Do204(context);                            
                        }
                    }

                    if(responseNextAction == ProxyNextAction.AllowButRequestContentInspection)
                    {
                        using(var upstreamResponseStream = await response.Content.ReadAsStreamAsync())
                        {
                            using(var ms = new MemoryStream())
                            {
                                await upstreamResponseStream.CopyToAsync(ms, 81920, context.RequestAborted);                              

                                var responseBody = ms.ToArray();

                                bool shouldBlockResponse = false;
                                responseBlockResponseContentType = string.Empty;
                                responseBlockResponse = null;
                                m_msgEndCb?.Invoke(reqUrl, resHeaderBuilder.ToString(), responseBody, context.Request.IsHttps ? MessageType.Https : MessageType.Http, MessageDirection.Response, out shouldBlockResponse, out responseBlockResponseContentType, out responseBlockResponse);

                                if(shouldBlockResponse)
                                {
                                    if(responseBlockResponse != null)
                                    {
                                        // User wants to block this response with a custom response.
                                        await DoCustomResponse(context, responseBlockResponseContentType, responseBlockResponse);
                                    }
                                    else
                                    {
                                        // User wants to block this response with a generic 204 response.
                                        Do204(context);
                                    }

                                    return;
                                }

                                // User inspected but allowed the content. Just write to the response
                                // body and then move on with your life fam.
                                //
                                // However, don't try to write a body if it's zero length. Also, do not try
                                // to write a body, even  if present, if the status is 204. Kestrel will not
                                // let us do this, and so far I can't find a way to remove this technically correct
                                // strict-compliance.
                                if(responseBody.Length > 0 && context.Response.StatusCode != 204)
                                {
                                    // If the request is HTTP1.0, we need to pull all the data so we can properly
                                    // set the content-length by adding the header in.
                                    if(upstreamReqVersionMatch != null && upstreamReqVersionMatch.Major == 1 && upstreamReqVersionMatch.Minor == 0)
                                    {
                                        context.Response.Headers.Add("Content-Length", responseBody.Length.ToString());
                                    }

                                    await context.Response.Body.WriteAsync(responseBody, 0, responseBody.Length);
                                }
                                else
                                {
                                    if(responseHasZeroContentLength)
                                    {
                                        context.Response.Headers.Add("Content-Length", "0");
                                    }
                                }

                                // Ensure we exit here, because if we fall past this scope then the
                                // response is going to get mangled.
                                return;
                            }
                        }
                    }
                }

                // If we made it here, then the user just wants to let the response be streamed in
                // without any inspection etc, so do exactly that.
                using(var responseStream = await response.Content.ReadAsStreamAsync())
                {
                    if(upstreamReqVersionMatch != null && upstreamReqVersionMatch.Major == 1 && upstreamReqVersionMatch.Minor == 0)
                    {
                        using(var ms = new MemoryStream())
                        {
                            await responseStream.CopyToAsync(ms, 81920, context.RequestAborted);

                            var responseBody = ms.ToArray();

                            context.Response.Headers.Add("Content-Length", responseBody.Length.ToString());

                            await context.Response.Body.WriteAsync(responseBody, 0, responseBody.Length);
                        }
                    }
                    else
                    {
                        if(responseHasZeroContentLength)
                        {
                            context.Response.Headers.Add("Content-Length", "0");
                        }
                        else
                        {
                            await responseStream.CopyToAsync(context.Response.Body, 81920, context.RequestAborted);
                        }
                    }
                }
            }
            catch(Exception e)
            {
                if(!(e is TaskCanceledException) && !(e is OperationCanceledException))
                {   
                    // Ignore task cancelled exceptions.
                    LoggerProxy.Default.Error(e);
                }
            }
        }

        /// <summary>
        /// Will put a 204 response into the context. Nothing more. 
        /// </summary>
        /// <param name="context">
        /// The request context. 
        /// </param>
        private void Do204(HttpContext context)
        {
            context.Response.Headers.Clear();
            context.Response.StatusCode = 204;            
            context.Response.Headers.Add("Expires", new Microsoft.Extensions.Primitives.StringValues(s_EpochHttpDateTime));
        }

        /// <summary>
        /// Will write the content to the response stream. 
        /// </summary>
        /// <param name="context">
        /// The request context. 
        /// </param>
        /// <param name="contentType">
        /// The content type for the data we're going to write as a response. 
        /// </param>
        /// <param name="customResponseBody">
        /// The raw response content. 
        /// </param>
        /// <returns>
        /// A task just cuz tbh fam smh. 
        /// </returns>
        private async Task DoCustomResponse(HttpContext context, string contentType, byte[] customResponseBody)
        {
            using(var ms = new MemoryStream(customResponseBody))
            {
                ms.Position = 0;
                context.Response.Headers.Clear();
                context.Response.StatusCode = 200;                
                context.Response.Headers.Add("Expires", new Microsoft.Extensions.Primitives.StringValues(s_EpochHttpDateTime));                
                context.Response.ContentType = contentType;
                await ms.CopyToAsync(context.Response.Body, 81920, context.RequestAborted);                
            }
        }
    }
}