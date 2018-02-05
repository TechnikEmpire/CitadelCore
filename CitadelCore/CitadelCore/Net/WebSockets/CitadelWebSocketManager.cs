/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System;
using System.Collections.Generic;
using System.Net.WebSockets;
//using System.Net.WebSockets.Managed;
using System.Text;
using System.Threading;

namespace CitadelCore.Net.WebSockets
{
    /// <summary>
    /// This class keeps track of web socket connections for CitadelCore so that it can pass control back to the filter handler and still get the web socket info it needs.
    /// </summary>
    public class CitadelWebSocketManager
    {
        public static CitadelWebSocketManager Default { get; set; }

        static CitadelWebSocketManager()
        {
            Default = new CitadelWebSocketManager();
        }

        public CitadelWebSocketManager()
        {
            negotiatedWebSockets = new Dictionary<WebSocket, WebSocketInfo>();
        }

        public void AddNegotiatedSocketPair(WebSocket clientSideSocket, WebSocketInfo webSocketInfo, CancellationToken requestAborted)
        {
            negotiatedWebSockets.Add(clientSideSocket, webSocketInfo);

            requestAborted.Register(() =>
            {
                negotiatedWebSockets.Remove(clientSideSocket);
            });
        }

        public WebSocketInfo GetNegotiatedSocket(WebSocket clientSideSocket)
        {
            WebSocketInfo webSocketInfo = null;
            negotiatedWebSockets.TryGetValue(clientSideSocket, out webSocketInfo);
            return webSocketInfo;
        }

        private Dictionary<WebSocket, WebSocketInfo> negotiatedWebSockets; // TODO: Also needs to keep track of reqHeaders strings.
    }

    public class WebSocketInfo
    {
        /// <summary>
        /// This socket is the one connected to the actual server.
        /// </summary>
        public System.Net.WebSockets.Managed.ClientWebSocket ClientSocket { get; set; }

        /// <summary>
        /// Request headers to help filter.
        /// </summary>
        public string RequestHeaders { get; set; }
    }
}
