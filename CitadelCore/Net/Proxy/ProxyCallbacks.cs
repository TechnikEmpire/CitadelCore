/*
* Copyright © 2017-Present Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using CitadelCore.IO;
using CitadelCore.Net.Http;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace CitadelCore.Net.Proxy
{
    /// <summary>
    /// Callback used to make filtering and internet-access decisions for a new traffic flow based on
    /// the application and the port in use.
    /// </summary>
    /// <param name="request">
    /// Firewall request. This contains information about the flow in question.
    /// </param>
    /// <returns>
    /// The response to the firewall inquiry, including the action to take for the given flow, and
    /// possibly for any such future flows that match the same criteria.
    /// </returns>
    public delegate FirewallResponse FirewallCheckCallback(FirewallRequest request);

    /// <summary>
    /// Delegate for handling new connections.
    /// </summary>
    /// <param name="messageInfo">
    /// The message information object.
    /// </param>
    public delegate void NewHttpMessageHandler(HttpMessageInfo messageInfo);

    /// <summary>
    /// Delegate for handling a requested stream inspection.
    /// </summary>
    /// <param name="messageInfo">
    /// The message information object.
    /// </param>
    /// <param name="operation">
    /// The operation performed on the inspected stream.
    /// </param>
    /// <param name="buffer">
    /// The data that passed through the stream.
    /// </param>
    /// <param name="dropConnection">
    /// Whether or not to immediately terminate the stream.
    /// </param>
    public delegate void HttpMessageStreamedInspectionHandler(HttpMessageInfo messageInfo, StreamOperation operation, Memory<byte> buffer, out bool dropConnection);

    /// <summary>
    /// Delegate for handling a requested inspection of the full, accumulated in-memory message body.
    /// </summary>
    /// <param name="messageInfo">
    /// The message information object.
    /// </param>
    public delegate void HttpMessageWholeBodyInspectionHandler(HttpMessageInfo messageInfo);

    /// <summary>
    /// Delegate for invoking a a command to terminate a replay inspection.
    /// </summary>
    /// <param name="closeSourceStream">
    /// Whether or not to close the source stream when terminating the replay.
    /// </param>
    /// <remarks>
    /// A replay can be terminated without closing the source stream. When terminating and indicating
    /// that the source stream should NOT be closed, we're simply stopping the duplication of the
    /// source stream data. When terminating and indicating that the source stream SHOULD be closed,
    /// then the original stream and the replay stream will be immediately terminated.
    /// </remarks>
    public delegate void HttpReplayTerminationCallback(bool closeSourceStream);

    /// <summary>
    /// Delegate for receiving a response to a replay inspection request.
    /// </summary>
    /// <param name="messageInfo">
    /// The message info of the source stream being replayed.
    /// </param>
    /// <param name="replayUrl">
    /// The replay URL to connect to in order to receive an exact replay of the source data HTTP
    /// response. Note that presently, only HTTP responses are supported.
    /// </param>
    /// <param name="cancellationCallback">
    /// A provided delegate that can be used to immediately terminate the replay, and optionally the
    /// source stream.
    /// </param>
    /// <remarks>
    /// This delegate is to be used for exceptional inspection that requires third party processes or
    /// to read the full HTTP response from a real HTTP server directly.
    ///
    /// Note that presently, only HTTP responses are supported. This is mostly due to the fact that
    /// responses are where the "good stuff" is usually happening, and also that there may be issues
    /// with Kestrel already scooping up and processing the request payload before invoking the
    /// handler, which negates the purpose of this kind of inspection.
    ///
    /// For more information, see the remarks on <seealso cref="ProxyNextAction.AllowButRequestResponseReplay" />.
    /// </remarks>
    public delegate void HttpMessageReplayInspectionHandler(HttpMessageInfo messageInfo, string replayUrl, HttpReplayTerminationCallback cancellationCallback);

    /// <summary>
    /// Delegate for delegating request/response handling to the library user.
    /// </summary>
    /// <param name="messageInfo">
    /// The message info object.
    /// </param>
    /// <param name="context">
    /// The HTTP context with which to fulfill the request and/or response.
    /// </param>
    /// <returns>
    /// A completion task.
    /// </returns>
    public delegate Task HttpExternalRequestHandler(HttpMessageInfo messageInfo, HttpContext context);
}