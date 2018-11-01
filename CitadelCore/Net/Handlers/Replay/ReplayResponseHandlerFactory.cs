/*
* Copyright © 2018-Present Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using CitadelCore.IO;
using CitadelCore.Net.Http;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CitadelCore.Net.Handlers.Replay
{
    /// <summary>
    /// The FilterResponseHandlerFactory returns specialized connection handlers.
    /// </summary>
    internal class ReplayResponseHandlerFactory
    {
        /// <summary>
        /// Gets the IPV4 endpoint where HTTP replay requests can be sent to.
        /// </summary>
        public IPEndPoint V4HttpEndpoint
        {
            internal set;
            get;
        }

        /// <summary>
        /// Gets the IPV4 endpoint where HTTPS replay requests can be sent to/
        /// </summary>
        public IPEndPoint V4HttpsEndpoint
        {
            internal set;
            get;
        }

        /// <summary>
        /// A collection of response replays scheduled for this handler.
        /// </summary>
        /// <remarks>
        /// Every <seealso cref="HttpMessageInfo" /> object has a
        /// <seealso cref="HttpMessageInfo.MessageId" /> that is unique. That is used as the key for
        /// this dictionary. The value is a <seealso cref="ResponseReplay" /> object that delicately
        /// manages receiving data from the main proxy, and then serving it up as a "replayed" HTTP response.
        /// </remarks>
        private ConcurrentDictionary<uint, ResponseReplay> _replays = new ConcurrentDictionary<uint, ResponseReplay>();

        /// <summary>
        /// This background worker runs for the lifetime of this object, which is the lifetime of the
        /// host process, and prunes orphaned replays from the <seealso cref="_replays" /> object.
        /// </summary>
        /// <remarks>
        /// It's definitely possible for a library user to request a replay and then abandon it. The
        /// object should get cleaned up in this case, but it doesn't get removed from the container here.
        /// </remarks>
        private BackgroundWorker _orphanPruningWorker;

        /// <summary>
        /// Private constructor to enforce singleton.
        /// </summary>
        internal ReplayResponseHandlerFactory()
        {
            _orphanPruningWorker = new BackgroundWorker();
            _orphanPruningWorker.DoWork += OrphanPrunerWork;
            _orphanPruningWorker.RunWorkerCompleted += OrphanPrunerWorkComplete;
            _orphanPruningWorker.WorkerReportsProgress = false;
            _orphanPruningWorker.WorkerSupportsCancellation = false;
            _orphanPruningWorker.RunWorkerAsync();
        }

        private void OrphanPrunerWorkComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            // Do nothing I guess?
        }

        /// <summary>
        /// DoWork callback for <seealso cref="_orphanPruningWorker" />.
        /// </summary>
        /// <param name="sender">
        /// Sender. Ignored.
        /// </param>
        /// <param name="e">
        /// Args. Ignored.
        /// </param>
        private async void OrphanPrunerWork(object sender, DoWorkEventArgs e)
        {
            var delay = TimeSpan.FromMinutes(1);
            do
            {
                try
                {
                    await Task.Delay(delay);

                    // Call ToArray to get a clone of the collection. We need do this so we don't get
                    // an error about updating the collection while iterating over it.
                    var allItems = _replays.ToArray();

                    foreach (var item in allItems)
                    {
                        if (item.Value.BodyComplete || item.Value.ReplayAborted || item.Value.SourceAborted)
                        {
                            _replays.TryRemove(item.Key, out ResponseReplay ignored);
                        }
                    }
                }
                catch { }
            }
            while (true);
        }

        /// <summary>
        /// Creates a new replay object and enqueues it for requesting.
        /// </summary>
        /// <param name="messageInfo">
        /// The message info.
        /// </param>
        /// <param name="cancellationToken">
        /// The source stream cancellation token.
        /// </param>
        /// <returns>
        /// A newly constructed relay object.
        /// </returns>
        public ResponseReplay CreateReplay(HttpMessageInfo messageInfo, CancellationToken cancellationToken)
        {
            var replay = new ResponseReplay(V4HttpEndpoint, messageInfo, cancellationToken);

            // Fun fact I didn't know - the indexer is atomic and thread safe. So much easier than
            // AddOrUpdate w/ func!
            _replays[messageInfo.MessageId] = replay;

            return replay;
        }

        /// <summary>
        /// Constructs and returns the appropriate handler for the supplied HTTP context.
        /// </summary>
        /// <param name="context">
        /// The HTTP content.
        /// </param>
        /// <returns>
        /// The new, specialized handler for the given context.
        /// </returns>
        public ReplayHttpResponseHandler GetHandler(HttpContext context)
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                return HandleWebsocket(context);
            }

            return HandleHttp(context);
        }

        /// <summary>
        /// Constructs a new handler specially for websocket contexts.
        /// </summary>
        /// <param name="context">
        /// The HTTP context.
        /// </param>
        /// <returns>
        /// The new, specialized handler for the given context.
        /// </returns>
        private ReplayHttpResponseHandler HandleWebsocket(HttpContext context)
        {
            return null;
        }

        /// <summary>
        /// Constructs a new handler specially for HTTP/S contexts.
        /// </summary>
        /// <param name="context">
        /// The HTTP context.
        /// </param>
        /// <returns>
        /// The new, specialized handler for the given context.
        /// </returns>
        private ReplayHttpResponseHandler HandleHttp(HttpContext context)
        {
            // Use helper to get the full, proper URL for the request.
            var fullUrl = Microsoft.AspNetCore.Http.Extensions.UriHelper.GetEncodedUrl(context.Request);

            var lastPath = fullUrl.LastIndexOf('/');
            if (lastPath > -1 && lastPath < (fullUrl.Length - 1))
            {
                var replayId = fullUrl.Substring(lastPath + 1);

                if (uint.TryParse(replayId, out uint replayIdInt))
                {
                    if (_replays.TryRemove(replayIdInt, out ResponseReplay replay))
                    {
                        return new ReplayHttpResponseHandler(replay);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Constructs a new handler specially for unknown protocol contexts.
        /// </summary>
        /// <param name="context">
        /// The HTTP context.
        /// </param>
        /// <returns>
        /// Destroys the whole universe. This handler is not implemented so it throws an exception.
        /// Not used.
        /// </returns>
        private AbstractFilterResponseHandler HandleUnknownProtocol(HttpContext context)
        {
            return null;
        }
    }
}