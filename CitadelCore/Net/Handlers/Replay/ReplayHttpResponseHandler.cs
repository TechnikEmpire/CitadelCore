/*
* Copyright © 2018-Present Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using CitadelCore.Extensions;
using CitadelCore.IO;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CitadelCore.Net.Handlers.Replay
{
    /// <summary>
    /// The FilterResponseHandlerFactory returns specialized connection handlers for replaying
    /// mirrored responses.
    /// </summary>
    internal class ReplayHttpResponseHandler
    {
        /// <summary>
        /// The replay object.
        /// </summary>
        private readonly ResponseReplay _replay;

        /// <summary>
        /// Constructs a FilterHttpResponseHandler instance.
        /// </summary>
        /// <param name="replay">
        /// The response replay object.
        /// </param>
        public ReplayHttpResponseHandler(
            ResponseReplay replay
            )
        {
            _replay = replay;
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
        public async Task Handle(HttpContext context)
        {
            await context.Response.ApplyMessageInfo(_replay.MessageInfo, context.RequestAborted);

            var waitPeriod = TimeSpan.FromMilliseconds(10);

            do
            {
                if (_replay.TryReadBody(out List<byte[]> data))
                {
                    foreach (var payload in data)
                    {
                        await context.Response.Body.WriteAsync(payload, 0, payload.Length);
                    }
                }
                else
                {
                    await Task.Delay(waitPeriod);
                }
            } while ((_replay.HasMoreData || !_replay.BodyComplete) && !_replay.ReplayAborted);

            await context.Response.Body.FlushAsync();
        }
    }
}