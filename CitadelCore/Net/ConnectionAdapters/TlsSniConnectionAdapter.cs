/*
* Copyright © 2017-Present Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using CitadelCore.Crypto;
using CitadelCore.Extensions;
using CitadelCore.IO;
using CitadelCore.Logging;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core.Adapter.Internal;
using StreamExtended;
using StreamExtended.Network;
using System;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace CitadelCore.Net.ConnectionAdapters
{
    /// <summary>
    /// The TlsSniConnectionAdapter handles SNI parsing on newly connected clients by faking a peek
    /// read on initial connection. The class then also handles certificate spoofing, and finally
    /// attemtping to complete the TLS handshake with the downstream client.
    /// </summary>
    internal class TlsSniConnectionAdapter : IConnectionAdapter
    {
        public bool IsHttps
        {
            get;
            private set;
        }

        /// <summary>
        /// Holds our certificate store. This is responsible for spoofing, storing and retrieving TLS certificates.
        /// </summary>
        private ISpoofedCertificateStore _certStore;

        /// <summary>
        /// Returned whenever we're forcing the connection closed, due to error.
        /// </summary>
        private static readonly ClosedAdaptedConnection s_closedConnection = new ClosedAdaptedConnection();

        /// <summary>
        /// Permitted TLS protocols.
        /// </summary>
        /// <remarks>
        /// We enable weak/bad protocols here because some clients in the world still like to use
        /// completely compromised encryption. For example, I once saw a company who's banking
        /// application still uses SSL3 to transfer vast sums of money automatically to and from the
        /// business. The application was not coded to use any newer protocol. So, while they are
        /// dirty, they are NOT enabled on the upstream end, so if anything, we're doing clients a
        /// favour here because the upstream connection will get seamlessly upgraded to standard/safe
        /// protocols while the bad client can still function.
        /// </remarks>
        private static readonly SslProtocols s_allowedTlsProtocols = SslProtocols.Ssl2 | SslProtocols.Ssl3 | SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12;

        /// <summary>
        /// Constructs a new TslSniConnectionAdapater instance.
        /// </summary>
        /// <param name="store">
        /// The certificate store to use for spoofing certificates.
        /// </param>
        public TlsSniConnectionAdapter(ISpoofedCertificateStore store)
        {
            _certStore = store;
        }

        public Task<IAdaptedConnection> OnConnectionAsync(ConnectionAdapterContext context)
        {
            return Task.Run(() => InnerOnConnectionAsync(context));
        }

        private async Task<IAdaptedConnection> InnerOnConnectionAsync(ConnectionAdapterContext context)
        {
            try
            {
                // We start off by handing the connection stream off to a library that can do a peek
                // read (which is really just doing buffering tricks, not an actual peek read).
                var clientStream = new CustomBufferedStream(context.ConnectionStream, 4096);

                // We then use the same lib to parse the "peeked" data and extract the SNI hostname.
                var clientSslHelloInfo = await SslTools.PeekClientHello(clientStream);

                switch (clientSslHelloInfo != null)
                {
                    case true:
                        {
                            IsHttps = true;

                            string sniHost = clientSslHelloInfo.Extensions?.FirstOrDefault(x => x.Name == "server_name")?.Data;

                            if (string.IsNullOrEmpty(sniHost) || string.IsNullOrWhiteSpace(sniHost))
                            {
                                return s_closedConnection;
                            }

                            try
                            {
                                var sslStream = new SslStream(clientStream, true,
                                    (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) =>
                                    {
                                        // TODO - Handle client certificates. They should be pushed
                                        // to the upstream connection eventually.
                                        if (certificate != null)
                                        {
                                        }

                                        return true;
                                    }
                                    );

                                // Spoof a cert for the extracted SNI hostname.
                                var spoofedCert = _certStore.GetSpoofedCertificateForHost(sniHost);

                                try
                                {
                                    // Try to handshake.
                                    await sslStream.AuthenticateAsServerAsync(spoofedCert, false, s_allowedTlsProtocols, true);
                                }
                                catch (OperationCanceledException oe)
                                {
                                    LoggerProxy.Default.Error("Failed to complete client TLS handshake because the operation was cancelled.");

                                    LoggerProxy.Default.Error(oe);

                                    sslStream.Dispose();
                                    return null;
                                }
                                catch (IOException ex)
                                {
                                    LoggerProxy.Default.Error("Failed to complete client TLS handshake because of IO exception.");

                                    LoggerProxy.Default.Error(ex);

                                    sslStream.Dispose();
                                    return null;
                                }

                                // Always set the feature even though the cert might be null
                                context.Features.Set<ITlsConnectionFeature>(new TlsConnectionFeature
                                {
                                    ClientCertificate = sslStream.RemoteCertificate?.ToV2Certificate()
                                });

                                return new HttpsConnection(sslStream);
                            }
                            catch (Exception err)
                            {
                                LoggerProxy.Default.Error("Failed to complete client TLS handshake because of unknown exception.");
                                LoggerProxy.Default.Error(err);
                            }

                            return null;
                        }

                    default:
                        {
                            IsHttps = false;
                            return new HttpConnection(clientStream);
                        }
                }
            }
            catch (Exception err)
            {
                LoggerProxy.Default.Error(err);

                return s_closedConnection;
            }
        }

        private class HttpConnection : IAdaptedConnection
        {
            private readonly Stream _plainHttpStream;

            public HttpConnection(Stream stream)
            {
                _plainHttpStream = stream;
            }

            public Stream ConnectionStream => _plainHttpStream;

            public void Dispose()
            {
                _plainHttpStream.Dispose();
            }
        }

        private class HttpsConnection : IAdaptedConnection
        {
            private readonly SslStream _sslStream;

            public HttpsConnection(SslStream sslStream)
            {
                _sslStream = sslStream;
            }

            public Stream ConnectionStream => _sslStream;

            public void Dispose()
            {
                _sslStream.Dispose();
            }
        }

        private class ClosedAdaptedConnection : IAdaptedConnection
        {
            public Stream ConnectionStream { get; } = new ClosedStream();

            public void Dispose()
            {
            }
        }
    }
}