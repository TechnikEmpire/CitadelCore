/*
* Copyright © 2018-Present Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using CitadelCore.Net.Http;
using System;
using System.IO;

namespace CitadelCore.IO
{
    /// <summary>
    /// The InspectionStream serves as a simple wrapper around another stream object, with callbacks
    /// that indicate when a read or write action is executed along with what the IO data is.
    /// </summary>
    internal class InspectionStream : Stream
    {
        /// <summary>
        /// Called whenever data is read from the stream.
        /// </summary>
        internal StreamIOHandler StreamRead;

        /// <summary>
        /// Called whenever data is written to the stream.
        /// </summary>
        internal StreamIOHandler StreamWrite;

        /// <summary>
        /// Called whenever the stream is closed.
        /// </summary>
        internal StreamIOHandler StreamClosed;

        /// <summary>
        /// Indicates whether the stream supports reading.
        /// </summary>
        public override bool CanRead
        {
            get
            {
                return _innerStream.CanRead;
            }
        }

        /// <summary>
        /// Indicates whether the stream supports seeking.
        /// </summary>
        public override bool CanSeek
        {
            get
            {
                return _innerStream.CanSeek;
            }
        }

        /// <summary>
        /// Indicates whether the stream supports writing.
        /// </summary>
        public override bool CanWrite
        {
            get
            {
                return _innerStream.CanWrite;
            }
        }

        /// <summary>
        /// Gets the length of the stream in bytes.
        /// </summary>
        public override long Length
        {
            get
            {
                return _innerStream.Length;
            }
        }

        /// <summary>
        /// Gets or sets the position in the stream.
        /// </summary>
        public override long Position
        {
            get
            {
                return _innerStream.Position;
            }

            set
            {
                _innerStream.Position = value;
            }
        }

        /// <summary>
        /// The inner stream object we wrap.
        /// </summary>
        private Stream _innerStream;

        /// <summary>
        /// The message info for the stream.
        /// </summary>
        public readonly HttpMessageInfo MessageInfo;

        /// <summary>
        /// Flag we use to determine whether or not to drop the connection.
        /// </summary>
        private bool _dropConnection = false;

        /// <summary>
        /// Constructs a new InspectionStream instance.
        /// </summary>
        /// <param name="messageInfo">
        /// The message info.
        /// </param>
        /// <param name="innerStream">
        /// The inner stream object.
        /// </param>
        public InspectionStream(HttpMessageInfo messageInfo, Stream innerStream)
        {
            MessageInfo = messageInfo;
            _innerStream = innerStream;
        }

        /// <summary>
        /// Flushes the stream.
        /// </summary>
        public override void Flush()
        {
            _innerStream.Flush();
        }

        /// <summary>
        /// Reads a sequence of bytes from the stream.
        /// </summary>
        /// <param name="buffer">
        /// The buffer to read in to.
        /// </param>
        /// <param name="offset">
        /// The offset within the buffer to read in to.
        /// </param>
        /// <param name="count">
        /// The maximum number of bytes to read.
        /// </param>
        /// <returns>
        /// The number of bytes read from the stream.
        /// </returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            var numRead = _innerStream.Read(buffer, offset, count);

            StreamRead?.Invoke(this, new Memory<byte>(buffer, offset, numRead), out _dropConnection);

            CheckHandleDropFlag();

            return numRead;
        }

        /// <summary>
        /// Gets the position in the current stream.
        /// </summary>
        /// <param name="offset">
        /// The offset amount to seek by.
        /// </param>
        /// <param name="origin">
        /// The seek origin.
        /// </param>
        /// <returns>
        /// The new position within the stream.
        /// </returns>
        public override long Seek(long offset, SeekOrigin origin)
        {
            return _innerStream.Seek(offset, origin);
        }

        /// <summary>
        /// Sets the length of the stream in bytes.
        /// </summary>
        /// <param name="value">
        /// The length.
        /// </param>
        public override void SetLength(long value)
        {
            _innerStream.SetLength(value);
        }

        /// <summary>
        /// Writes a sequence of bytes to the stream.
        /// </summary>
        /// <param name="buffer">
        /// The buffer to write.
        /// </param>
        /// <param name="offset">
        /// The offset within the buffer to write in to the stream.
        /// </param>
        /// <param name="count">
        /// The maximum number of bytes to write.
        /// </param>
        /// <returns>
        /// The number of bytes written in to the stream.
        /// </returns>
        public override void Write(byte[] buffer, int offset, int count)
        {
            StreamWrite?.Invoke(this, new Memory<byte>(buffer, offset, count), out _dropConnection);

            CheckHandleDropFlag();

            _innerStream.Write(buffer, offset, count);
        }

        /// <summary>
        /// Invokes the <seealso cref="StreamClosed" /> callback.
        /// </summary>
        public override void Close()
        {
            StreamClosed?.Invoke(this, new Memory<byte>(), out _dropConnection);
            base.Close();
        }

        /// <summary>
        /// Checks the current state of the <see cref="_dropConnection" /> flag and then
        /// force-disposes the inner stream if it is set.
        /// </summary>
        private void CheckHandleDropFlag()
        {
            if (_dropConnection)
            {
                // This is pretty cold-blooded but it's about the only option we have from inside
                // here. We have error handling that will prevent any exceptions this raises from
                // crashing the program though so there ya go.
                try
                {
                    _innerStream.Close();
                }
                catch { }

                try
                {
                    _innerStream.Dispose();
                }
                catch { }

                _innerStream = new ClosedStream();
            }
        }

        /// <summary>
        /// Private flag for avoiding repeat calls to <see cref="Dispose(bool)" />
        /// </summary>
        private volatile bool _disposed = false;

        /// <summary>
        /// Overrides the IDisposable interface of the base stream object.
        /// </summary>
        /// <param name="disposing">
        /// Whether or not we are disposing.
        /// </param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                if (_innerStream != null)
                {
                    _innerStream.Dispose();
                    _innerStream = null;
                }

                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Handler for the read and write events of the <seealso cref="InspectionStream" /> class.
    /// </summary>
    /// <param name="stream">
    /// The originating stream object.
    /// </param>
    /// <param name="buffer">
    /// The buffer that was read or written to the stream.
    /// </param>
    /// <param name="dropConnection">
    /// An out parameter the user sets to instruct whether or not the stream should be immediately terminated.
    /// </param>
    internal delegate void StreamIOHandler(InspectionStream stream, Memory<byte> buffer, out bool dropConnection);
}