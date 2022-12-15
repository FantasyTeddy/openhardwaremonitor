/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2012 Prince Samuel <prince.samuel@gmail.com>
  Copyright (C) 2012-2013 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using OpenHardwareMonitor.GUI;

namespace OpenHardwareMonitor.WebServer
{
    public sealed class RemoteWebServer : IDisposable
    {
        private IWebHost _host;

        public RemoteWebServer(Node node)
        {
            DataController.Root = node;
        }

        public void Start(int port)
        {
            IWebHostBuilder builder = WebHost.CreateDefaultBuilder()
                .UseStartup<Startup>()
                .UseUrls($"http://*:{port}");
            _host = builder.Build();

            _host.Start();
        }

        public void Stop()
        {
            _host?.StopAsync().Wait();
            _host?.Dispose();
        }

        public void Dispose()
        {
            _host?.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}
