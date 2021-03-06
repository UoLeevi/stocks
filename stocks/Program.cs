﻿using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace stocks
{
    public class Program
    {
        public static void Main(
            string[] args)
        {
            IConfigurationRoot config = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();

            config = new ConfigurationBuilder()
                .AddJsonFile(config["API_KEYS"])
                .AddConfiguration(config)
                .Build();

            IWebHost host = new WebHostBuilder()
                .UseKestrel(options => options.ListenLocalhost(5099))
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseStartup<Startup>()
                .UseConfiguration(config)
                .Build();

            host.Run();
        }
    }
}
