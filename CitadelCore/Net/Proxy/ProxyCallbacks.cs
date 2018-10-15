/*
* Copyright © 2017-Present Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using CitadelCore.IO;
using CitadelCore.Net.Http;
using System;

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
}