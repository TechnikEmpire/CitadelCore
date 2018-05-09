/*
* Copyright © 2017-Present Jesse Nicholson
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
    /// This enum provides greater control over internet access on a per-application basis to library
    /// users. With this enum, users can now decide which applications can be filtered as well as
    /// which applications are even allowed to use the internet, an in a port-specific way.
    /// </summary>
    public enum FirewallAction
    {
        /// <summary>
        /// Instructs the filtering engine to not filter the application at all, but to allow it to
        /// access the internet.
        /// </summary>
        DontFilterApplication,

        /// <summary>
        /// Instructs the filtering engine to filter the specified application on the specified port. 
        /// </summary>
        FilterApplication,

        /// <summary>
        /// Instructs the filtering engine to block all internet access for the application on the
        /// specified port.
        /// </summary>
        BlockInternetForApplication
    }

    /// <summary>
    /// A response to a firewall inquiry for a specific traffic flow.
    /// </summary>
    public class FirewallResponse
    {
        /// <summary>
        /// The action to take.
        /// </summary>
        public FirewallAction Action
        {
            get;
            private set;
        }

        /// <summary>
        /// Optional encryption hint. This enables the client to provide a hint to the filtering
        /// engine as to whether or not some application on some non-standard port is using HTTPS
        /// encryption. If this is set to a non-null value, and the flow is on a non-standard port,
        /// then the engine will handle the flow according to this value.
        /// </summary>
        public bool? EncryptedHint
        {
            get;
            private set;
        }

        /// <summary>
        /// Constructs a new FirewallResponse with the given action. 
        /// </summary>
        /// <param name="action">
        /// The action to take. 
        /// </param>
        /// <param name="encryptHint">
        /// Optional encryption hint. This enables the client to provide a hint to the filtering
        /// engine as to whether or not some application on some non-standard port is using HTTPS
        /// encryption. If this is set to a non-null value, and the flow is on a non-standard port,
        /// then the engine will handle the flow according to this value.
        /// </param>
        public FirewallResponse(FirewallAction action, bool? encryptHint = null)
        {
            Action = action;
            EncryptedHint = encryptHint;
        }
    }

    /// <summary>
    /// A reques tto a firewall inquiry for a specific traffic flow.
    /// </summary>
    public class FirewallRequest
    {
        /// <summary>
        /// The absolute path to the binary associated with the flow. 
        /// </summary>
        /// <remarks>
        /// May simply be SYSTEM if the path cannot be resolved, such as in the case that the process
        /// behind a flow is a system process.
        /// </remarks>
        public string BinaryAbsolutePath
        {
            get;
            private set;
        }

        /// <summary>
        /// The local port associated with the flow.
        /// </summary>
        public ushort LocalPort
        {
            get;
            private set;
        }

        /// <summary>
        /// The remote port associated with the flow.
        /// </summary>
        public ushort RemotePort
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets whether or not the process associated with the flow is a system process. 
        /// </summary>
        /// <remarks>
        /// In the event that the process is a system process, the <see cref="BinaryAbsolutePath" />
        /// variable will say "SYSTEM" rather than point to a path.
        /// </remarks>
        public bool IsSystemProcess
        {
            get
            {
                return BinaryAbsolutePath != null && BinaryAbsolutePath.Equals("system", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Constructs a new FirewallRequest object with the given binary path and local port. 
        /// </summary>
        /// <param name="binaryAbsolutePath">
        /// The absolute path to the binary that the flow relates to.
        /// </param>
        /// <param name="localPort">
        /// The local port associated with the flow.
        /// </param>
        /// <param name="remotePort">
        /// The remote port associated with the flow.
        /// </param>
        public FirewallRequest(string binaryAbsolutePath, ushort localPort, ushort remotePort)
        {
            BinaryAbsolutePath = binaryAbsolutePath;
            LocalPort = localPort;
            RemotePort = remotePort;
        }
    }

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