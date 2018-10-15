/*
* Copyright © 2018-Present Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System;

namespace CitadelCore.Util
{
    internal static class TimeUtil
    {
        /// <summary>
        /// Unix Epoch.
        /// </summary>
        public static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1);

        /// <summary>
        /// Unix Epoch string, for cache control.
        /// </summary>
        public static readonly string UnixEpochString = UnixEpoch.ToString("r");
    }
}