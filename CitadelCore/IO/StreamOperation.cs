/*
* Copyright © 2018-Present Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

namespace CitadelCore.IO
{
    /// <summary>
    /// Represents an operation performed on an inspected stream.
    /// </summary>
    public enum StreamOperation
    {
        /// <summary>
        /// The stream was read from.
        /// </summary>
        Read,

        /// <summary>
        /// The stream was written to.
        /// </summary>
        Write,

        /// <summary>
        /// The stream was closed.
        /// </summary>
        Close
    }
}