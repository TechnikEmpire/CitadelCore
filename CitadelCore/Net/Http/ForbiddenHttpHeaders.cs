/*
* Copyright © 2017-Present Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System;
using System.Collections.Generic;

namespace CitadelCore.Net.Http
{
    internal static class ForbiddenHttpHeaders
    {
        /// <summary>
        /// List of headers that cause headaches for proxies like me.
        /// </summary>
        private static readonly HashSet<string> s_forbidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "X-SDHC",
            "Avail-Dictionary",
            "Content-Length",
            "Content-Encoding",
            "Alternate-Protocol",
            "Alt-Svc",
            "Public-Key-Pins",
            "Public-Key-Pins-Report-Only",
            "Get-Dictionary",
            "Accept-Encoding",
            "Transfer-Encoding"
        };

        /// <summary>
        /// Whether or not the given header name is a forbidden header.
        /// </summary>
        /// <param name="headerName">
        /// The header name.
        /// </param>
        /// <returns>
        /// True if the header named is forbidden, false otherwise.
        /// </returns>
        public static bool IsForbidden(string headerName)
        {
            if (headerName == null)
            {
                return true;
            }

            return s_forbidden.Contains(headerName);
        }
    }
}