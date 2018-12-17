/*
* Copyright © 2018-Present Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using CitadelCore.IO;
using CitadelCore.Net.Proxy;
using CitadelCore.Util;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Net.Http;
using System.Threading;

namespace CitadelCore.Net.Http
{
    /// <summary>
    /// The HttpMessageInfo class communicates intent and data about a connection between the
    /// filtering library and library users.
    /// </summary>
    /// <remarks>
    /// When using this class, the process of ensuring that all properties are correct is a burden
    /// passed on to the user. For example, when setting a user-defined payload, the user MUST set
    /// <seealso cref="MessageType" /> to <seealso cref="MessageType.Response" /> for that payload to
    /// be properly written to the stream in question. Any headers must be checked and set properly
    /// by the user, status code, so on and so forth.
    /// </remarks>
    public class HttpMessageInfo
    {
        /// <summary>
        /// Used to increment the <seealso cref="MessageId" /> property.
        /// </summary>
        /// <remarks>
        /// Interlocked.Increment will roll over the value, and then our cast to uint will ensure
        /// that we have an infinitely recycleable unique ID.
        /// </remarks>
        private static long s_messageIdGen = 0;

        /// <summary>
        /// Gets or sets the originating message URL.
        /// </summary>
        public Uri Url
        {
            get;
            set;
        } = null;

        /// <summary>
        /// Gets the unique message Id.
        /// </summary>
        /// <remarks>
        /// Various api's, such as the stream inspection API, do not provide us with a way to persist
        /// and track unique connections outside of the library. So, this property was added to
        /// enable this.
        /// </remarks>
        public uint MessageId
        {
            get;
            internal set;
        } = (uint)Interlocked.Increment(ref s_messageIdGen);

        /// <summary>
        /// The originating request message, if any.
        /// </summary>
        /// <remarks>
        /// In the event that this message represents a request, this property will ne null. If this
        /// message represents a response, then this property will contain the original request
        /// message information.
        /// </remarks>
        public HttpMessageInfo OriginatingMessage
        {
            get;
            internal set;
        } = null;

        /// <summary>
        /// Gets or sets a custom <seealso cref="HttpClient" /> with which to fulfill the request.
        /// </summary>
        /// <remarks>
        /// Valid only when the <seealso cref="HttpMessageInfo.MessageType" /> is equal to <seealso cref="MessageType.Request" />.
        /// </remarks>
        public HttpClient FulfillmentClient
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the message method.
        /// </summary>
        public HttpMethod Method
        {
            get;
            set;
        } = HttpMethod.Get;

        /// <summary>
        /// Gets or sets the status code.
        /// </summary>
        /// <remarks>
        /// Only applies to responses.
        /// </remarks>
        public HttpStatusCode StatusCode
        {
            get;
            set;
        } = HttpStatusCode.OK;

        /// <summary>
        /// Gets the HTTP version used for this message.
        /// </summary>
        /// <remarks>
        /// This is provided as a read-only object because users cannot randomly change the protocol
        /// in the middle of a transaction. This information is provided because it is the
        /// responsibility of the user, when modifying data, to make sure that headers, as well as
        /// all other properties, are set correctly. This will aid library users in setting
        /// protocol-specific headers, for example.
        /// </remarks>
        public Version HttpVersion
        {
            get;
            internal set;
        } = new Version(1, 0);

        /// <summary>
        /// Gets the message headers.
        /// </summary>
        public NameValueCollection Headers
        {
            get;
            internal set;
        } = new NameValueCollection();

        /// <summary>
        /// Gets or sets the body content type.
        /// </summary>
        /// <remarks>
        /// Users may set this property directly, and it will only be referred to if the user also
        /// specifies that <seealso cref="ProxyNextAction" /> is set to
        /// <seealso cref="ProxyNextAction.DropConnection" /> AND the <seealso cref="Body" />
        /// property is set to a valid content buffer. So, if the user means to write a custom
        /// response to the message, they should set both this property AND the Body property.
        /// </remarks>
        public string BodyContentType
        {
            get;
            set;
        } = string.Empty;

        /// <summary>
        /// Private member for <see cref="Body" />
        /// </summary>
        private Memory<byte> _bodyMemory = new Memory<byte>();

        /// <summary>
        /// Gets the message body, if any.
        /// </summary>
        /// <remarks>
        /// Users may set this property directly, as long as they are preserving memory themselves,
        /// or as long as they are simply slicing and then assigning existing memory. <seealso cref="CopyAndSetBody(byte[], int, int, string)" />
        /// </remarks>
        public Memory<byte> Body
        {
            get
            {
                return _bodyMemory;
            }

            set
            {
                _bodyMemory = value;
                BodyIsUserCreated = true;
            }
        }

        /// <summary>
        /// Internal getter/setter for <see cref="Body" />
        /// </summary>
        internal Memory<byte> BodyInternal
        {
            get
            {
                return _bodyMemory;
            }

            set
            {
                _bodyMemory = value;
                BodyIsUserCreated = false;
            }
        }

        /// <summary>
        /// Gets whether or not the current body property is user-assigned.
        /// </summary>
        public bool BodyIsUserCreated
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the message protocol.
        /// </summary>
        public MessageProtocol MessageProtocol
        {
            get;
            internal set;
        } = MessageProtocol.Http;

        /// <summary>
        /// Gets whether or not the connection is encrypted.
        /// </summary>
        public bool IsEncrypted
        {
            get;
            internal set;
        } = false;

        /// <summary>
        /// Gets the message type.
        /// </summary>
        /// <remarks>
        /// The burden of ensuring that this property is set correctly, as well as all other
        /// properties, is on the user alone.
        /// </remarks>
        public MessageType MessageType
        {
            get;
            set;
        } = MessageType.Request;

        /// <summary>
        /// Gets the local IP address of the message.
        /// </summary>
        public IPAddress LocalAddress
        {
            get;
            internal set;
        } = null;

        /// <summary>
        /// Gets the local port of the message.
        /// </summary>
        public ushort LocalPort
        {
            get;
            internal set;
        } = 0;

        /// <summary>
        /// Gets the remote IP address of the message.
        /// </summary>
        public IPAddress RemoteAddress
        {
            get;
            internal set;
        } = null;

        /// <summary>
        /// Gets the remote port of the message.
        /// </summary>
        public ushort RemotePort
        {
            get;
            internal set;
        } = 0;

        /// <summary>
        /// A list of headers that can be populated which should be exempted from forbidden-header removal.
        /// </summary>
        public HashSet<string> ExemptedHeaders
        {
            get;
            internal set;
        } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets or sets the next action that the proxy should take for this message.
        /// </summary>
        public ProxyNextAction ProxyNextAction
        {
            get;
            set;
        } = ProxyNextAction.AllowAndIgnoreContentAndResponse;

        /// <summary>
        /// Private member that might optionally be used for <see cref="Body" />, but only when the
        /// user explicitly sets the body with the available public function.
        /// </summary>
        private byte[] _body = null;

        /// <summary>
        /// Constructs a new HttpMessageInfo instance.
        /// </summary>
        public HttpMessageInfo()
        {
        }

        /// <summary>
        /// Copies the supplied buffer to the message information object to preserve its life, and
        /// sets the public <see cref="Body" /> member to appropriately wrap the buffer as described
        /// by the given offset and count parameters.
        /// </summary>
        /// <param name="bodyData">
        /// The custom body data to write.
        /// </param>
        /// <param name="offset">
        /// The offset in the buffer to use.
        /// </param>
        /// <param name="count">
        /// The count in the buffer to use.
        /// </param>
        /// <param name="contentType">
        /// The Content-Type of the body.
        /// </param>
        /// <remarks>
        /// When a user wants to write a custom body in a request or a response, they can use this
        /// method to copy in and preserve that data across the library boundary. Note that if the
        /// user does not need this class to preserve the lifetime of the buffer, they may directly
        /// set the <see cref="Body" /> parameter.
        /// </remarks>
        public void CopyAndSetBody(byte[] bodyData, int offset, int count, string contentType)
        {
            if (_body != null)
            {
                Array.Clear(_body, 0, _body.Length);
            }

            BodyContentType = contentType ?? string.Empty;

            _body = bodyData;

            if (bodyData != null)
            {
                Body = _body;
            }
            else
            {
                Body = new Memory<byte>();
            }
        }

        /// <summary>
        /// Convenience function to transform the message information to reflect a 302 temporary
        /// redirect response.
        /// </summary>
        /// <param name="location">
        /// The location to redirect to.
        /// </param>
        public void MakeTemporaryRedirect(string location)
        {
            StatusCode = HttpStatusCode.Redirect;
            MessageType = MessageType.Response;
            Headers?.Clear();
            BodyContentType = string.Empty;

            if (_body != null && _body.Length > 0)
            {
                Array.Clear(_body, 0, _body.Length);
            }

            Body = new Memory<byte>();

            if (Headers != null)
            {
                Headers["Expires"] = TimeUtil.UnixEpochString;
                Headers["Location"] = location;
            }
        }

        /// <summary>
        /// Convenience function to transform the message information to reflect a 204 No-Content response.
        /// </summary>
        public void Make204NoContent()
        {
            StatusCode = HttpStatusCode.NoContent;
            MessageType = MessageType.Response;
            Headers.Clear();
            BodyContentType = string.Empty;

            if (_body != null && _body.Length > 0)
            {
                Array.Clear(_body, 0, _body.Length);
            }

            Body = new Memory<byte>();

            Headers["Expires"] = TimeUtil.UnixEpochString;
        }
    }
}