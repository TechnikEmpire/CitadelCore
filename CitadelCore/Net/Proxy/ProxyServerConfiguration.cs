/*
* Copyright © 2018-Present Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System.Net;
using System.Net.Http;

namespace CitadelCore.Net.Proxy
{
    /// <summary>
    /// The ProxyServerConfiguration class is used to configure a ProxyServer instance during construction.
    /// </summary>
    public class ProxyServerConfiguration
    {
        /// <summary>
        /// The authority name to use when issuing CA certificates.
        /// </summary>
        /// <remarks>
        /// This can simply be your company name.
        /// </remarks>
        public string AuthorityName
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets whether or not the proxy server will drop all external proxy server
        /// connections on the local machine.
        /// </summary>
        /// <remarks>
        /// This is set once, and persists for the lifetime of the proxy server.
        /// </remarks>
        public bool BlockExternalProxies
        {
            get;
            set;
        } = true;

        /// <summary>
        /// An optional, custom message handler for the upstream proxy connections.
        /// </summary>
        /// <remarks>
        /// You can use this to do things like proxy your proxy connections upstream, etc.
        /// </remarks>
        public HttpMessageHandler CustomProxyHandler
        {
            get;
            set;
        } = null;

        /// <summary>
        /// The firewall callback.
        /// </summary>
        public FirewallCheckCallback FirewallCheckCallback
        {
            get;
            set;
        }

        /// <summary>
        /// The callback for handling new HTTP messages.
        /// </summary>
        public NewHttpMessageHandler NewHttpMessageHandler
        {
            get;
            set;
        }

        /// <summary>
        /// The callback for handling requested complete, in-memory, HTTP message body inspections.
        /// </summary>
        public HttpMessageWholeBodyInspectionHandler HttpMessageWholeBodyInspectionHandler
        {
            get;
            set;
        }

        /// <summary>
        /// The callback for handling requested streamed content inspection.
        /// </summary>
        public HttpMessageStreamedInspectionHandler HttpMessageStreamedInspectionHandler
        {
            get;
            set;
        }

        /// <summary>
        /// The callback for handling request replay content inspection.
        /// </summary>
        public HttpMessageReplayInspectionHandler HttpMessageReplayInspectionCallback
        {
            get;
            set;
        }

        /// <summary>
        /// The callback for delegating request fullfillment to the library user, when request.
        /// </summary>
        public HttpExternalRequestHandler HttpExternalRequestHandlerCallback
        {
            get;
            set;
        }

        /// <summary>
        /// Gets whether or not the configuration is valid.
        /// </summary>
        public bool IsValid
        {
            get
            {
                return
                    FirewallCheckCallback != null &&
                    NewHttpMessageHandler != null &&
                    HttpMessageWholeBodyInspectionHandler != null &&
                    HttpMessageStreamedInspectionHandler != null &&
                    HttpMessageReplayInspectionCallback != null &&
                    HttpExternalRequestHandlerCallback != null;
            }
        }

        /// <summary>
        /// Configures the proxy server to use the supplied string for the common name when issuing certificates.
        /// </summary>
        /// <param name="authorityName">
        /// The authority common name to use.
        /// </param>
        /// <returns>
        /// The chained configuration instance.
        /// </returns>
        /// <remarks>
        /// This can and probably should simply be your organization name.
        /// </remarks>
        public ProxyServerConfiguration UseAuthority(string authorityName)
        {
            this.AuthorityName = authorityName;
            return this;
        }

        /// <summary>
        /// Configures the proxy server to use the supplied firewall check callback.
        /// </summary>
        /// <param name="callback">
        /// The callback to use.
        /// </param>
        /// <returns>
        /// The chained configuration instance.
        /// </returns>
        public ProxyServerConfiguration WithFirewallCallback(FirewallCheckCallback callback)
        {
            this.FirewallCheckCallback = callback;
            return this;
        }

        /// <summary>
        /// Configures the proxy server to use the supplied new message callback.
        /// </summary>
        /// <param name="callback">
        /// The callback to use.
        /// </param>
        /// <returns>
        /// The chained configuration instance.
        /// </returns>
        public ProxyServerConfiguration WithNewMessageHandler(NewHttpMessageHandler callback)
        {
            this.NewHttpMessageHandler = callback;
            return this;
        }

        /// <summary>
        /// Configures the proxy server to use the supplied whole-body inspection callback.
        /// </summary>
        /// <param name="callback">
        /// The callback to use.
        /// </param>
        /// <returns>
        /// The chained configuration instance.
        /// </returns>
        public ProxyServerConfiguration WithWholeBodyInspectionHandler(HttpMessageWholeBodyInspectionHandler callback)
        {
            this.HttpMessageWholeBodyInspectionHandler = callback;
            return this;
        }

        /// <summary>
        /// Configures the proxy server to use the supplied streamed inspection callback.
        /// </summary>
        /// <param name="callback">
        /// The callback to use.
        /// </param>
        /// <returns>
        /// The chained configuration instance.
        /// </returns>
        public ProxyServerConfiguration WithStreamedInspectionHandler(HttpMessageStreamedInspectionHandler callback)
        {   
            this.HttpMessageStreamedInspectionHandler = callback;
            return this;
        }
    }
}