/*
* Copyright © 2017-Present Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using CitadelCore.IO;
using System.Net.Http;

namespace CitadelCore.Net.Proxy
{
    /// <summary>
    /// The ProxyNextAction enum defines actions available to external users during the processing of
    /// proxied connections.
    /// </summary>
    public enum ProxyNextAction
    {
        /// <summary>
        /// Lets the connection pass through without filtering at its current state.
        /// </summary>
        AllowAndIgnoreContent = 0,

        /// <summary>
        /// Allows the connection to proceed, but attempts to buffer the entire connection contents,
        /// which are to be passed back for inspection and further filtering.
        /// </summary>
        AllowButRequestContentInspection = 1,

        /// <summary>
        /// Allows the connection to proceed, but as the payload is written across the proxy,
        /// callbacks will be invoked for every buffered write called.
        /// </summary>
        /// <remarks>
        /// This action is useful for inspecting high volume streams, such as video streams. You
        /// cannot and indeed should not even try to preload a video for classification with the
        /// AllowButRequestContentInspection action. Instead, you can listen in on buffered contents
        /// of the stream as they "pass by" the proxy and, at each callback, decide whether or not
        /// the proxy should terminate the stream.
        /// </remarks>
        AllowButRequestStreamedContentInspection = 2,

        /// <summary>
        /// Immediately drops the connection with the supplied data. If no data supplied, a generic
        /// 204 No-Content will be sent to close the connection gracefully in the case of Http
        /// transactions. When the transaction is a websocket, the connection will simply be closed.
        /// </summary>
        DropConnection = 3,

        /// <summary>
        /// Allows the entire connection, including any response, to pass without any inspection or filtering.
        /// </summary>
        AllowAndIgnoreContentAndResponse = 4,

        /// <summary>
        /// Allows the connection to proceed, but in the appropriate callback, a localhost URL is
        /// given where the connection payload can be read from a local HTTP server and replayed
        /// exactly as it was received by the proxy. Along with a unique URL where a replay can be
        /// requested from, a callback is also supplied that enables the user to terminate the
        /// connection at any time.
        /// </summary>
        /// <remarks>
        /// What is this all about? What good reason can we possibly have for simply duplicating data
        /// in memory and piping it through another, second HTTP request and response? Simple. Some
        /// data may require more complex analysis that must involve a secondary processing mechanism.
        ///
        /// A perfect example of such a scenario is video playback, which also happens to be the
        /// primary motivator behind the addition of this capability. We already have the
        /// <seealso cref="InspectionStream" /> class, but trying to program something atop the
        /// <seealso cref="InspectionStream" /> to reconstruct all the various types of video that
        /// can be piped over TCP, and all the various protocols that can be used to transfer it, is
        /// a ludicrous proposition. It's simply an astronomical undertaking that I can't imagine
        /// succeeding, if working properly even most of the time is the definition of success here.
        ///
        /// So, by "replaying" the response, we can feed the returned URL to an external mechamism,
        /// like Windows Media Foundation, which will leverage it's own monolithic code to make the
        /// connection, determine the media type, load the codec (and pay the patent fees), manage
        /// the data stream and connection, leverage the GPU to decode things, and very nicely just
        /// hand us video and audio frames on request.
        ///
        /// This is the purpose of the response replay mechanism. In fact "replay" is only used here
        /// because of naming precendents in this field (think HTTP replay, packet replay). What's
        /// actually happening is that, assuming you connect to the given URL immediately, you are
        /// receiving a real-time duplication of the data, and as such can also terminate the stream
        /// before full completion if your inspection of the replay causes you to make this determination.
        /// </remarks>
        AllowButRequestResponseReplay = 5,

        /// <summary>
        /// This action enables users to fulfill the request or response at-will.
        /// </summary>
        AllowButDelegateHandler,
    }
}