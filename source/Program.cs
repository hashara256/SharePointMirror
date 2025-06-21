using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharePointMirror.Options;
using SharePointMirror.Services;
using System.Runtime.InteropServices;

namespace SharePointMirror
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                          .AddJsonFile(
                              $"appsettings.{hostingContext.HostingEnvironment.EnvironmentName}.json",
                              optional: true,
                              reloadOnChange: true
                          )
                          .AddEnvironmentVariables();
                })
                .ConfigureServices((context, services) =>
                {
                    // Bind configuration sections to strongly-typed options
                    services.Configure<SharePointOptions>(
                        context.Configuration.GetSection("SharePoint")
                    );
                    services.Configure<TrackingOptions>(
                        context.Configuration.GetSection("Tracking")
                    );

                    // Register application services and dependencies
                    services.AddSingleton<IAuthContextFactory, AuthContextFactory>();
                    services.AddSingleton<IFolderProcessor, FolderProcessor>();
                    services.AddSingleton<SharePointService>();

                    // Register the background worker service
                    services.AddHostedService<Worker>();
                });

            // Use Windows Service if running as a service on Windows
            if (!System.Diagnostics.Debugger.IsAttached && !Environment.UserInteractive && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                builder.UseWindowsService();
            }

            var host = builder.Build();
            await host.RunAsync();
        }
    }
}
