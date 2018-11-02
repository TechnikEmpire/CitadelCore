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
    /// <summary>
    /// Enforces a pattern of requiring message handlers on all filtering response handlers.
    /// </summary>
    internal abstract class AbstractFilterResponseHandler
    {
        /// <summary>
        /// For writing empty responses without new allocations.
        /// </summary>
        protected static readonly byte[] s_nullBody = new byte[0];

        /// <summary>
        /// Private, shared configuration instance that we use for user-defined handlers.
        /// </summary>
        protected readonly ProxyServerConfiguration _configuration;

        /// <summary>
        /// Constructs a AbstractFilterResponseHandler instance.
        /// </summary>
        /// <param name="configuration">
        /// The shared, proxy configuration.
        /// </param>
        /// <exception cref="ArgumentException">
        /// If the supplied configuration is null or invalid, this constructor will throw.
        /// </exception>
        public AbstractFilterResponseHandler(
            ProxyServerConfiguration configuration
            )
        {
            _configuration = configuration;

            if (_configuration == null || !_configuration.IsValid)
            {
                throw new ArgumentException("Configuration is null or invalid. Ensure it is defined, and that all callbacks are defined.");
            }
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
        public abstract Task Handle(HttpContext context);
    }
}