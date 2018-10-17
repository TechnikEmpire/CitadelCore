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
        /// <param name="newMessageCallback">
        /// Callback used for new messages.
        /// </param>
        /// <param name="wholeBodyInspectionCallback">
        /// Callback used when full-body content inspection is requested on a new message.
        /// </param>
        /// <param name="streamInspectionCallback">
        /// Callback used when streamed content inspection is requested on a new message.
        /// </param>
        /// <param name="replayInspectionCallback">
        /// Callback used when replay content inspection is requested on HTTP response message.
        /// </param>
        public FilterPassthroughResponseHandler(
            NewHttpMessageHandler newMessageCallback,
            HttpMessageWholeBodyInspectionHandler wholeBodyInspectionCallback,
            HttpMessageStreamedInspectionHandler streamInspectionCallback,
            HttpMessageReplayInspectionHandler replayInspectionCallback
            ) : base(newMessageCallback, wholeBodyInspectionCallback, streamInspectionCallback, replayInspectionCallback)
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