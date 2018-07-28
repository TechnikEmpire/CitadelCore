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
        /// The process ID of the application in question.
        /// </summary>
        public ulong ProcessId
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
        /// <param name="processId">
        /// The process ID of the application in question.
        /// </param>
        public FirewallRequest(string binaryAbsolutePath, ushort localPort, ushort remotePort, ulong processId)
        {
            BinaryAbsolutePath = binaryAbsolutePath;
            LocalPort = localPort;
            RemotePort = remotePort;
            ProcessId = processId;
        }
    }
}