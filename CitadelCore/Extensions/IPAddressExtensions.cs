/*
* Copyright © 2017-Present Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System.Net;

namespace CitadelCore.Extensions
{
    /// <summary>
    /// Extension methods for <seealso cref="IPAddress"/> instances.
    /// </summary>
    public static class IPAddressExtensions
    {
        /// <summary>
        /// Determines whether or not the address is an IPV4 private address.
        /// </summary>
        /// <param name="address">
        /// This address.
        /// </param>
        /// <returns>
        /// True if this address is IPV4 private, false otherwise.
        /// </returns>
        public static bool IsPrivateIpv4Address(this IPAddress address)
        {
            if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                return false;
            }

            var bytes = address.GetAddressBytes();

            return bytes.ContainsPrivateIpv4Address();
        }
    }
}