/*
* Copyright © 2017 Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System;

namespace CitadelCore.Net.Proxy
{
    /// <summary>
    /// Represents the type of message in inspection callbacks. 
    /// </summary>
    public enum MessageType
    {
        /// <summary>
        /// The message is plain, unencrypted HTTP.
        /// </summary>
        Http,

        /// <summary>
        /// The message is encrypted HTTP.
        /// </summary>
        Https,

        /// <summary>
        /// The message is a plain, unencrypted Websocket message.
        /// </summary>
        WebSocket,

        /// <summary>
        /// The message is an encrypted Websocket message.
        /// </summary>
        SecureWebSocket
    }

    /// <summary>
    /// Represents the direction of the message. That is to say, informs as to whether or not the
    /// message is a request or response message.
    /// </summary>
    public enum MessageDirection
    {
        /// <summary>
        /// The message is a request message, aka originating from the local client.
        /// </summary>
        Request,

        /// <summary>
        /// The message is a response message, aka originating from the remote server.
        /// </summary>
        Response
    }

    /// <summary>
    /// Callback used to determine if traffic associated with the binary at the supplied path should
    /// be passed through the proxy, which is equivalent to granting the binary internet access.
    /// Burden is on the user to ensure that binaries should have internet access.
    /// </summary>
    /// <param name="binaryAbsolutePath">
    /// Absolute path to the binary in question. 
    /// </param>
    /// <returns>
    /// User must return true if traffic from the binary at the specified path ought to have its
    /// traffic passed through the proxy, false otherwise.
    /// </returns>
    public delegate bool FirewallCheckCallback(string binaryAbsolutePath);

    /// <summary>
    /// Called when a message, be it HTTP or otherwise, has begin. At this stage, only the headers
    /// should be complete, if any, but under various circumstanges there may be no headers and only
    /// a body, or both headers and body may be defined fully. At this stage, the user has the
    /// opportunity to direct the proxy on how to handle this new transaction. The user may block it
    /// immediately, permit it to pass through but request the content for inspection, or permit it
    /// to pass through but request the response, if any is to come, for inspection.
    /// </summary>
    /// <param name="requestUrl">
    /// The full URL of the initiating request. Will always be defined.
    /// </param>
    /// <param name="headers">
    /// The message headers, if any. 
    /// </param>
    /// <param name="body">
    /// The message body, if any. There may be no body to a message. 
    /// </param>
    /// <param name="msgType">
    /// The message type. 
    /// </param>
    /// <param name="msgDirection">
    /// The message direction. 
    /// </param>
    /// <param name="nextAction">
    /// Out parameter where the next action to be taken on transaction represented in this callback
    /// can be determined.
    /// </param>
    /// <param name="customBlockResponseContentType">
    /// If you are providing a custom response body, you should/need to set the content type here as well.
    /// </param>
    /// <param name="customBlockResponse">
    /// If nextAction is set to block, you can optionally specify the response body to send before
    /// the connection is closed. If nextAction is block, and this parameter is set to null, and the
    /// transaction is HTTP, then a generic 204 No-Content will be sent to gracefully close the connection.
    /// </param>
    public delegate void MessageBeginCallback(Uri requestUrl, string headers, byte[] body, MessageType msgType, MessageDirection msgDirection, out ProxyNextAction nextAction, out string customBlockResponseContentType, out byte[] customBlockResponse);

    /// <summary>
    /// Called when a message, be it HTTP or otherwise, has completed. At this stage, the headers
    /// should be complete, if any, and any body that might be part of the message should be
    /// complete. Typically you'll only ever have this callback invoked if you've request to inspect
    /// a request or response payload. At this stage, the user only has the opporunity to block the
    /// transaction entirely, with or without a custom response, or to permit the transaction to
    /// complete without modification.
    /// </summary>
    /// <param name="requestUrl">
    /// The full URL of the initiating request. Will always be defined.
    /// </param>
    /// <param name="headers">
    /// The message headers, if any. 
    /// </param>
    /// <param name="body">
    /// The message body, if any. There may be no body to a message. 
    /// </param>
    /// <param name="msgType">
    /// The message type. 
    /// </param>
    /// <param name="msgDirection">
    /// The message direction. 
    /// </param>
    /// <param name="shouldBlock">
    /// Out parameter boolean that should be set to indicate whether or not the transaction
    /// represented here should be blocked.
    /// </param>
    /// <param name="customBlockResponseContentType">
    /// If you are providing a custom response body, you should/need to set the content type here as well.
    /// </param>
    /// <param name="customBlockResponse">
    /// If shouldBlock is set to true, you can optionally specify the response body to send before
    /// the connection is closed. If shouldBlock is set to true, and this parameter is set to null,
    /// and the transaction is HTTP, then a generic 204 No-Content will be sent to gracefully close
    /// the connection.
    /// </param>
    public delegate void MessageEndCallback(Uri requestUrl, string headers, byte[] body, MessageType msgType, MessageDirection msgDirection, out bool shouldBlock, out string customBlockResponseContentType, out byte[] customBlockResponse);
}