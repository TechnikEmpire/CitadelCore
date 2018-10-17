/*
* Copyright © 2017-Present Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using CitadelCore.Crypto;
using CitadelCore.Diversion;
using CitadelCore.Logging;
using CitadelCore.Net.ConnectionAdapters;
using CitadelCore.Net.Handlers;
using CitadelCore.Net.Handlers.Replay;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace CitadelCore.Net.Proxy
{
    /// <summary>
    /// The ProxyServer class holds the core, platform-independent filtering proxy logic.
    /// </summary>
    public abstract class ProxyServer
    {
        /// <summary>
        /// List of proxying web servers generated for this host. Currently there's always going to
        /// be two, one for IPV4 and one for IPV6.
        /// </summary>
        private List<IWebHost> _hosts = new List<IWebHost>();

        private IDiverter _diverter;

        /// <summary>
        /// The TlsSniConnetionAdapter that we use to peek the SNI extension from connected TLS
        /// clients, then spoof a certificate in order to establish a secure connection.
        /// </summary>
        private readonly TlsSniConnectionAdapter _tlsConnAdapter;

        /// <summary>
        /// Gets the IPV4 endpoint where HTTP connections are being received. This will be ANY:0
        /// until Start has been called.
        /// </summary>
        public IPEndPoint V4HttpEndpoint
        {
            private set;
            get;
        }

        /// <summary>
        /// Gets the IPV4 endpoint where HTTPS connections are being received. This will be ANY:0
        /// until Start has been called.
        /// </summary>
        public IPEndPoint V4HttpsEndpoint
        {
            private set;
            get;
        }

        /// <summary>
        /// Gets the IPV6 endpoint where HTTP connections are being received. This will be ANY:0
        /// until Start has been called.
        /// </summary>
        public IPEndPoint V6HttpEndpoint
        {
            private set;
            get;
        }

        /// <summary>
        /// Gets the IPV6 endpoint where HTTPS connections are being received. This will be ANY:0
        /// until Start has been called.
        /// </summary>
        public IPEndPoint V6HttpsEndpoint
        {
            private set;
            get;
        }

        /// <summary>
        /// Gets whether or not the server is currently running.
        /// </summary>
        public bool IsRunning
        {
            get
            {
                return _running;
            }
        }

        /// <summary>
        /// Ref held to firewall callback.
        /// </summary>
        private FirewallCheckCallback _fwCallback;

        /// <summary>
        /// Flag that indicates if we're running or not.
        /// </summary>
        private volatile bool _running = false;

        /// <summary>
        /// For synchronizing startup and shutdown.
        /// </summary>
        private readonly object _startStopLock = new object();

        /// <summary>
        /// Creates a new proxy server instance. Really there should only ever be a single instance
        /// created at a time.
        /// </summary>
        /// <param name="configuration">
        /// The proxy server configuration to use.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Will throw if any one of the callbacks in the supplied configuration are not defined.
        /// </exception>
        public ProxyServer(ProxyServerConfiguration configuration)
        {
            _tlsConnAdapter = new TlsSniConnectionAdapter(CreateCertificateStore(configuration.AuthorityName ?? "CitadelCore"));
            _fwCallback = configuration.FirewallCheckCallback ?? throw new ArgumentException("The firewall callback MUST be defined.", nameof(configuration));

            FilterResponseHandlerFactory.Default.NewMessageCallback = configuration.NewHttpMessageHandler ?? throw new ArgumentException("The new message callback MUST be defined.", nameof(configuration));
            FilterResponseHandlerFactory.Default.WholeBodyInspectionCallback = configuration.HttpMessageWholeBodyInspectionHandler ?? throw new ArgumentException("The whole-body content inspection callback MUST be defined.", nameof(configuration));
            FilterResponseHandlerFactory.Default.StreamedInspectionCallback = configuration.HttpMessageStreamedInspectionHandler ?? throw new ArgumentException("The streaming content inspection callback MUST be defined.", nameof(configuration));
            FilterResponseHandlerFactory.Default.ReplayInspectionCallback = configuration.HttpMessageReplayInspectionCallback ?? throw new ArgumentException("The replay content inspection callback MUST be defined.", nameof(configuration));

            // Hook the cert verification callback.
            ServicePointManager.ServerCertificateValidationCallback += CertificateVerificationHandler;
        }

        /// <summary>
        /// Creates a new SpoofedCertStore to be used with the proxy for secure connections.
        /// </summary>
        /// <param name="authorityCommonName">
        /// The common name to use when generating the certificate authority. Basically, all SSL
        /// sites will show that they are secured by a certificate authority with this name that is
        /// supplied here.
        /// </param>
        /// <returns>
        /// </returns>
        protected virtual ISpoofedCertificateStore CreateCertificateStore(string authorityCommonName)
        {
            return new SpoofedCertStore(authorityCommonName);
        }

        /// <summary>
        /// Starts the proxy server on both IPV4 and IPV6 address space.
        /// </summary>
        /// <param name="numThreads">
        /// An optional value that can be used to set the number of threads that the underlying
        /// packet diversion system will use for packet IO. Defaults to zero. If less than or equal
        /// to zero, the diverter should automatically choose to use one thread per logical core.
        /// However, given that diverters are platform specific, this is not guaranteed.
        /// </param>
        /// <exception cref="NullReferenceException">
        /// In the event that the internal kestrel engine doesn't properly initialize, this method
        /// will throw.
        /// </exception>
        public void Start(int numThreads = 0)
        {
            lock (_startStopLock)
            {
                if (_running)
                {
                    return;
                }

                // Create the public, v4 proxy.
                IPEndPoint v4HttpEndpoint = null;
                IPEndPoint v4HttpsEndpoint = null;

                var publicV4Host = CreateHost<PublicServerStartup>(false, false, out v4HttpEndpoint, out v4HttpsEndpoint);

                V4HttpEndpoint = v4HttpEndpoint;
                V4HttpsEndpoint = v4HttpsEndpoint;

                // Create the public, v6 proxy.
                IPEndPoint v6HttpEndpoint = null;
                IPEndPoint v6HttpsEndpoint = null;

                var publicV6Host = CreateHost<PublicServerStartup>(false, true, out v6HttpEndpoint, out v6HttpsEndpoint);

                V6HttpEndpoint = v6HttpEndpoint;
                V6HttpsEndpoint = v6HttpsEndpoint;

                // Create the private, v4 replay proxy
                IPEndPoint privateV4HttpEndpoint = null;
                IPEndPoint privateV4HttpsEndpoint = null;

                var privateV4Host = CreateHost<PrivateServerStartup>(true, false, out privateV4HttpEndpoint, out privateV4HttpsEndpoint);

                ReplayResponseHandlerFactory.Default.V4HttpEndpoint = privateV4HttpEndpoint;
                ReplayResponseHandlerFactory.Default.V4HttpsEndpoint = privateV4HttpsEndpoint;

                _hosts = new List<IWebHost>()
                {
                    publicV4Host,
                    publicV6Host,
                    privateV4Host
                };

                _diverter = CreateDiverter(
                        V4HttpEndpoint,
                        V4HttpsEndpoint,
                        V6HttpEndpoint,
                        V6HttpsEndpoint
                    );

                _diverter.ConfirmDenyFirewallAccess = (procPath) =>
                {
                    return _fwCallback.Invoke(procPath);
                };

                _diverter.Start(numThreads);

                _running = true;
            }
        }

        /// <summary>
        /// Internal call to create the platform specific packet diverter.
        /// </summary>
        /// <param name="ipv4HttpEp">
        /// The endpoint where the proxy is listening for IPV4 HTTP connections.
        /// </param>
        /// <param name="ipv4HttpsEp">
        /// The endpoint where the proxy is listening for IPV4 HTTPS connections.
        /// </param>
        /// <param name="ipv6HttpEp">
        /// The endpoint where the proxy is listening for IPV6 HTTP connections.
        /// </param>
        /// <param name="ipv6HttpsEp">
        /// The endpoint where the proxy is listening for IPV6 HTTPS connections.
        /// </param>
        /// <returns>
        /// The platform specific diverter.
        /// </returns>
        protected abstract IDiverter CreateDiverter(IPEndPoint ipv4HttpEp, IPEndPoint ipv4HttpsEp, IPEndPoint ipv6HttpEp, IPEndPoint ipv6HttpsEp);

        /// <summary>
        /// Stops any running proxy server listeners and allows them to be disposed.
        /// </summary>
        public void Stop()
        {
            lock (_startStopLock)
            {
                if (!_running)
                {
                    return;
                }

                foreach (var host in _hosts)
                {
                    host.StopAsync().Wait();
                }

                _hosts = new List<IWebHost>();

                _diverter.Stop();

                _running = false;
            }
        }

        /// <summary>
        /// Handles client certification verification.
        /// </summary>
        /// <param name="sender">
        /// Sender. Ignored.
        /// </param>
        /// <param name="certificate">
        /// The certificate to verify.
        /// </param>
        /// <param name="chain">
        /// The certificate chain.
        /// </param>
        /// <param name="sslPolicyErrors">
        /// Errors detected during preverification.
        /// </param>
        /// <returns>
        /// True if the certificate was verified, false otherwise.
        /// </returns>
        protected virtual bool CertificateVerificationHandler(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors != SslPolicyErrors.None)
            {
                try
                {
                    // We will tolerate certain chain status issues, such as a failure to reach the
                    // CRL server.
                    int acceptable = 0;
                    foreach (var element in chain.ChainStatus)
                    {
                        switch (element.Status)
                        {
                            case X509ChainStatusFlags.OfflineRevocation:
                            case X509ChainStatusFlags.RevocationStatusUnknown:
                                {
                                    // We're not going to break websites for people just because the
                                    // designated CRL is offline.
                                    ++acceptable;
                                }
                                break;
                        }
                    }

                    // If, and ONLY IF, all of our existing errors are acceptable, we will adjust the
                    // chain verification to tolerate those errors and re-run the chain building process.
                    if (acceptable > 0 && acceptable == chain.ChainStatus.Length)
                    {
                        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreCertificateAuthorityRevocationUnknown | X509VerificationFlags.IgnoreCtlSignerRevocationUnknown | X509VerificationFlags.IgnoreEndRevocationUnknown | X509VerificationFlags.IgnoreRootRevocationUnknown;
                        var asX2 = new X509Certificate2(certificate);
                        return chain.Build(asX2);
                    }
                }
                catch (Exception err)
                {
                    LoggerProxy.Default.Error(err);
                }

                return false;
            }

            return true;
        }

        /// <summary>
        /// Creates a web server that will bind to local addresses to any available port.
        /// </summary>
        /// <param name="isPrivate">
        /// Whether or not the host should bind to the loopback adapter, or a public interface.
        /// </param>
        /// <param name="isV6">
        /// Whether or not this server should be bound to a v6 address. We have yet to determine if
        /// we can even grab some internal of kestrel that will allow us to manipulate the listener
        /// socket endpoint to force it into dual mode, so we use this option instead and generate
        /// two hosts.
        /// </param>
        /// <param name="boundHttpEndpoint">
        /// The endpoint that the HTTP host was bound to.
        /// </param>
        /// <param name="boundHttpsEndpoint">
        /// The endpoint that the HTTPS host was bound to.
        /// </param>
        /// <returns>
        /// An IWebHost that will transparently proxy all requests.
        /// </returns>
        /// <exception cref="NullReferenceException">
        /// In the event that the internal kestrel engine doesn't properly initialize, this method
        /// will throw.
        /// </exception>
        private IWebHost CreateHost<T>(bool isPrivate, bool isV6, out IPEndPoint boundHttpEndpoint, out IPEndPoint boundHttpsEndpoint) where T : class
        {
            WebHostBuilder ipWebhostBuilder = new WebHostBuilder();

            ListenOptions httpListenOptions = null;
            ListenOptions httpsListenOptions = null;

            ipWebhostBuilder.UseSockets(opts =>
            {
                opts.IOQueueCount = 0;
            });

            // Use Kestrel server.
            ipWebhostBuilder.UseKestrel(opts =>
            {
                opts.Limits.MaxRequestBodySize = null;
                opts.Limits.MaxRequestBufferSize = null;
                opts.Limits.MaxConcurrentConnections = null;
                opts.Limits.MaxConcurrentUpgradedConnections = null;

                // Listen for HTTPS connections. Keep a reference to the options object so we can get
                // the chosen port number after we call start.
                opts.Listen(isV6 ? IPAddress.IPv6Any : IPAddress.Any, 0, listenOpts =>
                {
                    // Plug in our TLS connection adapter. This adapter will handle SNI parsing and
                    // certificate spoofing based on the SNI value.
                    listenOpts.ConnectionAdapters.Add(_tlsConnAdapter);

                    // Who doesn't love to kick that old Nagle to the curb?
                    listenOpts.NoDelay = true;

                    // HTTP 2 got cut last minute from 2.1 and MS speculates that it may take several
                    // releases to get it properly included.
                    // https://github.com/aspnet/Docs/issues/5242#issuecomment-380863456
                    // listenOpts.Protocols = HttpProtocols.Http1;

                    httpsListenOptions = listenOpts;
                });

                // Listen for HTTP connections. Keep a reference to the options object so we can get
                // the chosen port number after we call start.
                opts.Listen(isV6 ? IPAddress.IPv6Any : IPAddress.Any, 0, listenOpts =>
                {
                    // Who doesn't love to kick that old Nagle to the curb?
                    listenOpts.NoDelay = true;

                    // HTTP 2 got cut last minute from 2.1 and MS speculates that it may take several
                    // releases to get it properly included.
                    // https://github.com/aspnet/Docs/issues/5242#issuecomment-380863456
                    // listenOpts.Protocols = HttpProtocols.Http1;

                    httpListenOptions = listenOpts;
                });
            });

            // Add compression for responses.
            ipWebhostBuilder.ConfigureServices(serviceOpts =>
            {
                serviceOpts.AddResponseCompression();
            });

            ipWebhostBuilder.Configure(cfgApp =>
            {
                cfgApp.UseResponseCompression();
            });

            // Configures how we handle requests and errors, etc.
            ipWebhostBuilder.UseStartup<T>();

            // Build host. You needed this comment.
            var vHost = ipWebhostBuilder.Build();

            // Start the host. You definitely needed this. It's not until we start the host that the
            // listener endpoints will be resolved. We need that info so we know where our proxy
            // server is running, so we can divert packets to it.
            vHost.Start();

            // Since this is post vHost.Start(), we can now grab the EP of the connection.
            if (httpListenOptions != null)
            {
                boundHttpEndpoint = httpListenOptions.IPEndPoint;
                boundHttpsEndpoint = httpsListenOptions.IPEndPoint;
            }
            else
            {
                throw new NullReferenceException("httpListenOptions is expected to be non-null!");
            }

            return vHost;
        }

        /// <summary>
        /// Startup class for public IWebHosts. This configures the host for important things like
        /// how to handle errors and how to handle requests.
        /// </summary>
        private class PublicServerStartup
        {
            public PublicServerStartup(IHostingEnvironment env)
            {
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseResponseCompression();

                // We proxy websockets, so enable this.
                var wsOpts = new WebSocketOptions
                {
                    ReceiveBufferSize = (int)(ushort.MaxValue * 5)
                };
                app.UseWebSockets();

                // Exception handler. Not yet sure what to do here.
                app.UseExceptionHandler(
                    options =>
                    {
                        options.Run(
                            async context =>
                            {
                                try
                                {
                                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                                    context.Response.ContentType = "text/html";

                                    var ex = context.Features.Get<IExceptionHandlerFeature>();

                                    if (ex != null)
                                    {
                                        var err = $"<h1>Error: {ex.Error.Message}</h1>{ex.Error.StackTrace }";
                                        await context.Response.WriteAsync(err).ConfigureAwait(false);
                                    }
                                }
                                catch { }
                            }
                        );
                    }
                );

                // Global request handler. Terminates middleware, aka this is the final handler and
                // middleware will come before this. In the end, we simply ask our factory to give us
                // the appropriate handler given what the context us, and then let it return a task
                // we give back to kestrel to see through.
                app.Run(context =>
                {
                    return Task.Run(async () =>
                    {
                        var handler = FilterResponseHandlerFactory.Default.GetHandler(context);
                        await handler.Handle(context);
                    });
                });
            }
        }

        /// <summary>
        /// Startup class for private IWebHosts. This configures the host for important things like
        /// how to handle errors and how to handle requests.
        /// </summary>
        private class PrivateServerStartup
        {
            public PrivateServerStartup(IHostingEnvironment env)
            {
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseResponseCompression();

                // We proxy websockets, so enable this.
                var wsOpts = new WebSocketOptions
                {
                    ReceiveBufferSize = (int)(ushort.MaxValue * 5)
                };
                app.UseWebSockets();

                // Exception handler. Not yet sure what to do here.
                app.UseExceptionHandler(
                    options =>
                    {
                        options.Run(
                            async context =>
                            {
                                try
                                {
                                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                                    context.Response.ContentType = "text/html";

                                    var ex = context.Features.Get<IExceptionHandlerFeature>();

                                    if (ex != null)
                                    {
                                        var err = $"<h1>Error: {ex.Error.Message}</h1>{ex.Error.StackTrace }";
                                        await context.Response.WriteAsync(err).ConfigureAwait(false);
                                    }
                                }
                                catch { }
                            }
                        );
                    }
                );

                // Global request handler. Terminates middleware, aka this is the final handler and
                // middleware will come before this. In the end, we simply ask our factory to give us
                // the appropriate handler given what the context us, and then let it return a task
                // we give back to kestrel to see through.
                app.Run(context =>
                {
                    return Task.Run(async () =>
                    {
                        var handler = ReplayResponseHandlerFactory.Default.GetHandler(context);

                        if (handler != null)
                        {
                            await handler.Handle(context);
                        }
                        else
                        {
                            context.Abort();
                        }
                    });
                });
            }
        }
    }
}