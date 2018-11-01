/*
* Copyright © 2018-Present Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using CitadelCore.Net.Handlers;
using CitadelCore.Net.Http;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;

namespace CitadelCore.IO
{
    /// <summary>
    /// The response replay class acts as a temporary storage container that can have mirrored data
    /// written to it on one end, while the replay local proxy reads from it, in a concurrent fashion.
    /// </summary>
    internal class ResponseReplay
    {
        /// <summary>
        /// The message information.
        /// </summary>
        /// <remarks>
        /// This is needed almost exclusively for the headers.
        /// </remarks>
        public HttpMessageInfo MessageInfo
        {
            get;
            set;
        }

        /// <summary>
        /// This holds data being mirrored internally in the main proxy, which should then be written
        /// to the replay stream.
        /// </summary>
        private ConcurrentQueue<byte[]> _pendingBody = new ConcurrentQueue<byte[]>();

        /// <summary>
        /// Private member for <see cref="BodyComplete" />.
        /// </summary>
        private volatile bool _bodyComplete = false;

        /// <summary>
        /// This is used to set an upper limit on max data buffering size for the response replay.
        /// </summary>
        /// <remarks>
        /// We'll set it to about 65 megs so there's more than enough time between buffering and when
        /// the library user starts pulling data out of the stream.
        /// </remarks>
        private static readonly ulong _maxBufferSize = ushort.MaxValue * 1000;

        /// <summary>
        /// This is the cancellation token attached to the request being handled and mirror to us
        /// here by <seealso cref="FilterHttpResponseHandler" />. If this cancellation token is
        /// flagged, then it means the original request was aborted. This can used to quickly abort
        /// the replay process as well.
        /// </summary>
        private readonly CancellationToken _cancellationToken;

        /// <summary>
        /// A separate flag indicating that the replay alone has been aborted, while the source
        /// stream remains untouched.
        /// </summary>
        private volatile bool _replayAbortedOnly = false;

        /// <summary>
        /// Gets whether or not the source stream has aborted the connection.
        /// </summary>
        public bool SourceAborted
        {
            get
            {
                return _cancellationToken.IsCancellationRequested;
            }
        }

        /// <summary>
        /// Gets or sets whether or not the replay itself has been aborted.
        /// </summary>
        /// <remarks>
        /// This is independent of <seealso cref="SourceAborted" />.
        /// </remarks>
        public bool ReplayAborted
        {
            get
            {
                return _replayAbortedOnly;
            }

            set
            {
                _replayAbortedOnly = value;
            }
        }

        /// <summary>
        /// Gets or sets whether or not the entire body has been streamed through this replay.
        /// </summary>
        /// <remarks>
        /// This flag is set internally by <seealso cref="FilterHttpResponseHandler" /> whenever the
        /// <seealso cref="InspectionStream" /> object mirroring the body data has indicated that the
        /// stream has closed. The stream will be closed automatically by Kestrel upon completion, so
        /// this should be reliable.
        /// </remarks>
        public bool BodyComplete
        {
            get
            {
                return _bodyComplete;
            }

            set
            {
                _bodyComplete = value;
            }
        }

        /// <summary>
        /// Gets whether or not any data is in the queue.
        /// </summary>
        public bool HasMoreData
        {
            get
            {
                return _pendingBody.Count > 0;
            }
        }

        /// <summary>
        /// Returns the total number of bytes currently in the queue.
        /// </summary>
        private ulong BodySize
        {
            get
            {
                return (ulong)_pendingBody.Select(x => x.Length).Sum();
            }
        }

        /// <summary>
        /// Gets the replay local URL from which it can be read.
        /// </summary>
        internal string ReplayUrl
        {
            get
            {
                return $"http://localhost:{_serverHttpEndpoint.Port}/replay/{MessageInfo.MessageId.ToString()}";
            }
        }

        /// <summary>
        /// The HTTP endpoint that the replay server can be reached at.
        /// </summary>
        private readonly IPEndPoint _serverHttpEndpoint;

        /// <summary>
        /// Constructs a new ResponseReplay instance with the given parameters.
        /// </summary>
        /// <param name="serverHttpEndpoint">
        /// The HTTP endpoint that the replay server is bound to.
        /// </param>
        /// <param name="messageInfo">
        /// The message info.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token from the original request handled by <seealso cref="FilterHttpResponseHandler" />.
        /// </param>
        /// <exception cref="ArgumentException">
        /// If the message info object is null, or is not a response, this constructor will throw.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// If the cancellation token is null, this constructor will throw.
        /// </exception>
        public ResponseReplay(IPEndPoint serverHttpEndpoint, HttpMessageInfo messageInfo, CancellationToken cancellationToken)
        {
            if (messageInfo == null || messageInfo.MessageType != MessageType.Response)
            {
                throw new ArgumentException("The information object must not be null and must indicate that it is a response.", nameof(messageInfo));
            }

            if (cancellationToken == null)
            {
                throw new ArgumentException("The cancellation token object must not be null.", nameof(cancellationToken));
            }

            _serverHttpEndpoint = serverHttpEndpoint ?? throw new ArgumentException("The server endpoint cannot be null.", nameof(cancellationToken));
            MessageInfo = messageInfo;
            _cancellationToken = cancellationToken;
        }

        /// <summary>
        /// Writes body/content bytes to the replay.
        /// </summary>
        /// <param name="bytes">
        /// The number of bytes to write to the FIFO queue.
        /// </param>
        /// <returns>
        /// True if the write was a success, false otherwise.
        /// </returns>
        /// <remarks>
        /// If a return value of false is given, that means that the write that failed would have
        /// exceeded the upper boundary of reserved memory of buffering the replay event. In this
        /// case, the engine will internally abandon the replay because the user is considered to
        /// have failed to act on the replay, and resources must be reclaimed.
        /// </remarks>
        internal bool WriteBodyBytes(System.Memory<byte> bytes)
        {
            if (BodySize > _maxBufferSize)
            {
                return false;
            }

            _pendingBody.Enqueue(bytes.ToArray());
            return true;
        }

        /// <summary>
        /// Attempts to read bytes from the current body message queue.
        /// </summary>
        /// <param name="bytes">
        /// Out parameter that is a list of all dequeued body arrays.
        /// </param>
        /// <returns>
        /// True if the operation was a success, false otherwise.
        /// </returns>
        /// <remarks>
        /// A return value of false may simply indicate that the replay is waiting on more mirrored
        /// data. Check the <seealso cref="BodyComplete" />, <see cref="SourceAborted" /> and
        /// <seealso cref="ReplayAborted" /> flags in this case.
        /// </remarks>
        internal bool TryReadBody(out List<byte[]> bytes)
        {
            var retVal = new List<byte[]>();

            bool success = false;

            do
            {
                success = _pendingBody.TryDequeue(out byte[] buffer);
                if (success)
                {
                    retVal.Add(buffer);
                }
            }
            while (success);

            bytes = retVal;

            return retVal.Count > 0;
        }
    }
}