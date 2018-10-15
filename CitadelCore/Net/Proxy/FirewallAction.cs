/*
* Copyright © 2017-Present Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

namespace CitadelCore.Net.Proxy
{
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
}