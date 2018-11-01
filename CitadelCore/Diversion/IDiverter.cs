/*
* Copyright © 2017-Present Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using CitadelCore.Net.Proxy;

namespace CitadelCore.Diversion
{
    /// <summary>
    /// Defines the interface for a platform-specific packet diverter.
    /// </summary>
    public interface IDiverter
    {
        /// <summary>
        /// Gets whether or not the diverter is presently operating and diverting traffic.
        /// </summary>
        bool IsRunning
        {
            get;
        }

        /// <summary>
        /// Gets or sets whether or not external proxies should be dropped during the packet
        /// diversion process.
        /// </summary>
        bool DropExternalProxies
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the callback that the Diverter is to use when checking to see if an
        /// application behind a packet flow should have it's internet content pushed through the
        /// proxy. This is called FirewallAccess because this inherently gives the application
        /// internet access via the proxy.
        /// </summary>
        FirewallCheckCallback ConfirmDenyFirewallAccess
        {
            get;
            set;
        }

        /// <summary>
        /// Starts the diversion process with a hint about how many threads ought to be used to
        /// process the diversion of packets.
        /// </summary>
        /// <param name="numThreads">
        /// Thread count hint. This may be ignored by the underlying implementation, but by design,
        /// defaulting this to 0 or less will indicate that Environment.ProcessorCount should be used
        /// to determine the total number of threads.
        /// </param>
        void Start(int numThreads);

        /// <summary>
        /// Stops diverting packets. This is a blocking call.
        /// </summary>
        void Stop();
    }
}