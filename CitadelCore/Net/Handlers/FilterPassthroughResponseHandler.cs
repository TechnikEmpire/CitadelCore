/*
* Copyright © 2017-Present Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using CitadelCore.Net.Proxy;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace CitadelCore.Net.Handlers
{
    internal class FilterPassthroughResponseHandler : AbstractFilterResponseHandler
    {
        /// <summary>
        /// Constructs a FilterPassthroughResponseHandler instance.
        /// </summary>
        /// <param name="configuration">
        /// The shared, proxy configuration.
        /// </param>
        /// <exception cref="ArgumentException">
        /// If the supplied configuration is null or invalid, this constructor will throw.
        /// </exception>
        public FilterPassthroughResponseHandler(
            ProxyServerConfiguration configuration
            ) : base(configuration)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Invoked when this handler is determined to be the best suited to handle the supplied connection.
        /// </summary>
        /// <param name="context">
        /// The HTTP context.
        /// </param>
        /// <returns>
        /// The handling task.
        /// </returns>
        public override Task Handle(HttpContext context)
        {
            throw new NotImplementedException();
        }
    }
}