/*
* Copyright © 2017 Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using CitadelCore.Diversion;
using CitadelCore.Net.ConnectionAdapters;
using CitadelCore.Net.Handlers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace CitadelCore.Net.Proxy
{
    
    /// <summary>
    /// The ProxyServer class holds the core, platform-independent filtering proxy logic. 
    /// </summary>
    public abstract class ProxyServer
    {
        // This will handle SNI and handshaking.
        private TlsSniConnectionAdapter m_tlsConnAdapter = new TlsSniConnectionAdapter();

        private IPEndPoint m_v4HttpListenerEp = new IPEndPoint(IPAddress.Any, 0);
        private IPEndPoint m_v4HttpsListenerEp = new IPEndPoint(IPAddress.Any, 0);

        private IPEndPoint m_v6HttpListenerEp = new IPEndPoint(IPAddress.IPv6Any, 0);
        private IPEndPoint m_v6HttpsListenerEp = new IPEndPoint(IPAddress.IPv6Any, 0);

        /// <summary>
        /// List of proxying web servers generated for this host. Currently there's always going to
        /// be two, one for IPV4 and one for IPV6.
        /// </summary>
        private List<IWebHost> m_hosts = new List<IWebHost>();

        private IDiverter m_diverter;

        /// <summary>
        /// Gets the IPV4 endpoint where HTTP connections are being received. This will be ANY:0
        /// until Start has been called.
        /// </summary>
        public IPEndPoint V4HttpEndpoint
        {
            get
            {
                return m_v4HttpListenerEp;
            }
        }

        /// <summary>
        /// Gets the IPV4 endpoint where HTTPS connections are being received. This will be ANY:0
        /// until Start has been called.
        /// </summary>
        public IPEndPoint V4HttpsEndpoint
        {
            get
            {
                return m_v4HttpListenerEp;
            }
        }

        /// <summary>
        /// Gets the IPV6 endpoint where HTTP connections are being received. This will be ANY:0
        /// until Start has been called.
        /// </summary>
        public IPEndPoint V6HttpEndpoint
        {
            get
            {
                return m_v6HttpListenerEp;
            }
        }

        /// <summary>
        /// Gets the IPV6 endpoint where HTTPS connections are being received. This will be ANY:0
        /// until Start has been called.
        /// </summary>
        public IPEndPoint V6HttpsEndpoint
        {
            get
            {
                return m_v6HttpListenerEp;
            }
        }

        /// <summary>
        /// Gets whether or not the server is currently running.
        /// </summary>
        public bool IsRunning
        {
            get
            {
                return m_running;
            }
        }

        /// <summary>
        /// Ref held to firewall callback.
        /// </summary>
        private FirewallCheckCallback m_fwCallback;

        /// <summary>
        /// Flag that indicates if we're running or not.
        /// </summary>
        private volatile bool m_running = false;

        /// <summary>
        /// For synchronizing startup and shutdown. 
        /// </summary>
        private object m_startStopLock = new object();

        /// <summary>
        /// Creates a new proxy server instance. Really there should only ever be a single instance
        /// created at a time.
        /// </summary>
        /// <param name="firewallCallback">
        /// The firewall check callback. Used to allow the user to determine if a binary should have
        /// its associated traffic pushed through the filter or not.
        /// </param>
        /// <param name="messageBeginCallback">
        /// Message begin callback enables users to inspect and filter messages immediately after
        /// they begin. Users also have the power to direct how the proxy will continue to handle the
        /// overall transaction that this message belongs to.
        /// </param>
        /// <param name="messageEndCallback">
        /// Message end callback enables users to inspect and filter messages once they have completed. 
        /// </param>
        /// <exception cref="ArgumentException">
        /// Will throw if any one of the callbacks are not defined. 
        /// </exception>
        public ProxyServer(FirewallCheckCallback firewallCallback, MessageBeginCallback messageBeginCallback, MessageEndCallback messageEndCallback)
        {
            if(firewallCallback == null)
            {
                throw new ArgumentException("The firewall callback MUST be defined.");
            }

            if(messageBeginCallback == null)
            {
                throw new ArgumentException("The message begin callback MUST be defined.");
            }

            if(messageEndCallback == null)
            {
                throw new ArgumentException("The message end callback MUST be defined.");
            }
            
            m_fwCallback = firewallCallback;
            FilterResponseHandlerFactory.Default.MessageBeginCallback = messageBeginCallback;
            FilterResponseHandlerFactory.Default.MessageEndCallback = messageEndCallback;            
        }

        /// <summary>
        /// Starts the proxy server on both IPV4 and IPV6 address space. 
        /// </summary>
        public void Start()
        {
            lock(m_startStopLock)
            {
                if(m_running)
                {
                    return;
                }

                m_hosts = new List<IWebHost>()
                {
                    CreateHost(false),
                    CreateHost(true)
                };

                m_diverter = CreateDiverter(
                        m_v4HttpListenerEp,
                        m_v4HttpsListenerEp,
                        m_v6HttpListenerEp,
                        m_v6HttpsListenerEp
                    );

                m_diverter.ConfirmDenyFirewallAccess = (procPath) =>
                {
                    return m_fwCallback.Invoke(procPath);
                };

                m_diverter.Start(0);

                m_running = true;
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
            lock(m_startStopLock)
            {
                if(!m_running)
                {
                    return;
                }

                foreach(var host in m_hosts)
                {
                    host.StopAsync().Wait();
                }

                m_hosts = new List<IWebHost>();

                m_diverter.Stop();

                m_running = false;
            }
        }

        /// <summary>
        /// Creates a web server that will bind to local addresses to any available port. 
        /// </summary>
        /// <param name="isV6">
        /// Whether or not this server should be bound to a v6 address. We have yet to determine if
        /// we can even grab some internal of kestrel that will allow us to manipulate the listener
        /// socket endpoint to force it into dual mode, so we use this option instead and generate
        /// two hosts.
        /// </param>
        /// <returns>
        /// An IWebHost that will transparently proxy all requests. 
        /// </returns>
        private IWebHost CreateHost(bool isV6)
        {
            WebHostBuilder ipWebhostBuilder = new WebHostBuilder();

            ListenOptions httpListenOptions = null;
            ListenOptions httpsListenOptions = null;

            ipWebhostBuilder.UseLibuv(opts =>
            {
                opts.ThreadCount = Environment.ProcessorCount;
            });

            // Use Kestrel server.
            ipWebhostBuilder.UseKestrel(opts =>
            {   
                // Listen for HTTPS connections. Keep a reference to the options object so we can get
                // the chosen port number after we call start.
                opts.Listen(isV6 ? IPAddress.IPv6Any : IPAddress.Any, 0, listenOpts =>
                {
                    // Plug in our TLS connection adapter. This adapter will handle SNI parsing and
                    // certificate spoofing based on the SNI value.
                    listenOpts.ConnectionAdapters.Add(m_tlsConnAdapter);

                    // Who doesn't love to kick that old Nagle to the curb?
                    listenOpts.NoDelay = true;
                    httpsListenOptions = listenOpts;
                });

                // Listen for HTTP connections. Keep a reference to the options object so we can get
                // the chosen port number after we call start.
                opts.Listen(isV6 ? IPAddress.IPv6Any : IPAddress.Any, 0, listenOpts =>
                {
                    // Who doesn't love to kick that old Nagle to the curb?
                    listenOpts.NoDelay = true;
                    httpListenOptions = listenOpts;
                });
            });
            
            // Configures how we handle requests and errors, etc.            
            ipWebhostBuilder.UseStartup<Startup>();
            
            // Build host. You needed this comment.
            var vHost = ipWebhostBuilder.Build();
            
            // Start the host. You definitely needed this. It's not until we start the host that the
            // listener endpoints will be resolved. We need that info so we know where our proxy
            // server is running, so we can divert packets to it.
            vHost.Start();

            // Since this is post vHost.Start(), we can now grab the EP of the connection.
            if(isV6)
            {
                m_v6HttpListenerEp = httpListenOptions.IPEndPoint;
                m_v6HttpsListenerEp = httpsListenOptions.IPEndPoint;
            }
            else
            {
                m_v4HttpListenerEp = httpListenOptions.IPEndPoint;
                m_v4HttpsListenerEp = httpsListenOptions.IPEndPoint;
            }

            return vHost;
        }

        /// <summary>
        /// Startup class for IWebHosts. This configures the host for important things like how to
        /// handle errors and how to handle requests.
        /// </summary>
        private class Startup
        {
            public Startup(IHostingEnvironment env)
            {
                
            }

            public void Configure(IApplicationBuilder app)
            {
                // We proxy websockets, so enable this.
                app.UseWebSockets();

                // Exception handler. Not yet sure what to do here.
                app.UseExceptionHandler(
                    options =>
                    {
                        options.Run(
                            async context =>
                            {
                                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                                context.Response.ContentType = "text/html";

                                var ex = context.Features.Get<IExceptionHandlerFeature>();

                                if(ex != null)
                                {
                                    var err = $"<h1>Error: {ex.Error.Message}</h1>{ex.Error.StackTrace }";
                                    await context.Response.WriteAsync(err).ConfigureAwait(false);
                                }
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
    }
}