/*
* Copyright © 2017-Present Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using CitadelCore.Net.Proxy;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace CitadelCore.Net.Handlers
{
    /// <summary>
    /// Enforces a pattern of requiring message handlers on all filtering response handlers. 
    /// </summary>
    internal abstract class AbstractFilterResponseHandler
    {
        /// <summary>
        /// Callback used for new messages.
        /// </summary>
        protected NewHttpMessageHandler _newMessageCb;

        /// <summary>
        /// Callback used when full-body content inspection is requested on a new message.
        /// </summary>
        protected HttpMessageWholeBodyInspectionHandler _wholeBodyInspectionCb;

        /// <summary>
        /// Callback used when streamed content inspection is requested on a new message.
        /// </summary>
        protected HttpMessageStreamedInspectionHandler _streamInpsectionCb;

        /// <summary>
        /// For writing empty responses without new allocations.
        /// </summary>
        protected static readonly byte[] s_nullBody = new byte[0];

        /// <summary>
        /// Constructs a AbstractFilterResponseHandler instance.
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
        public AbstractFilterResponseHandler(NewHttpMessageHandler newMessageCallback, HttpMessageWholeBodyInspectionHandler wholeBodyInspectionCallback, HttpMessageStreamedInspectionHandler streamInspectionCallback)
        {
            _newMessageCb = newMessageCallback;
            _wholeBodyInspectionCb = wholeBodyInspectionCallback;
            _streamInpsectionCb = streamInspectionCallback;
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