# Citadel Core
Cross platform filtering HTTP/S proxy based on .NET Standard 2.0.

CitadelCore supports proxying of both HTTP/S and Websocket (Ws/Wss) connections. As of v1.4.2, CitadelCore's Websocket proxy passes the [Autobahn Test Suite](https://github.com/TechnikEmpire/CitadelCore/releases/download/v1.4.2/autobahn-testsuite-results.zip).

Note that CitadelCore is an abstract library by design. Since the proxy is designed to be run as a local, transparent filtering proxy, a platform-specific mechanism for diverting traffic back into the proxy must be implemented on each platform. All other logic is implemented in this library. 

In short, that means this library bundles a [full-fledged, compliant HTTP/S web server](https://github.com/aspnet/KestrelHttpServer) and has handlers that bridge (aka proxy) those webserver requests to the real upstream target. At various stages of transactions in that pipline, exposed callbacks are invoked enabling users to perform deep content inspection, connection filtering, and content modification.

For the Windows specific implementation, please see [CitadelCore.Windows](https://github.com/TechnikEmpire/CitadelCore.Windows).

[![Build Status](https://travis-ci.org/TechnikEmpire/CitadelCore.svg?branch=master)](https://travis-ci.org/TechnikEmpire/CitadelCore)
<a href="https://scan.coverity.com/projects/technikempire-citadelcore">
  <img alt="Coverity Scan Build Status"
       src="https://scan.coverity.com/projects/15514/badge.svg"/>
</a>
[![Codacy Badge](https://api.codacy.com/project/badge/Grade/79dbc8edcb3a413eafc84d0e506342e0)](https://www.codacy.com/app/TechnikEmpire/CitadelCore?utm_source=github.com&amp;utm_medium=referral&amp;utm_content=TechnikEmpire/CitadelCore&amp;utm_campaign=Badge_Grade)
![NugetLinkBadge](https://img.shields.io/nuget/v/CitadelCore.svg)
![NugetDownloadsBadge](https://img.shields.io/nuget/dt/CitadelCore.svg)  
