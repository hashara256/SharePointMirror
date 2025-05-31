using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharePointMirror.Options;
using SharePointMirror.Services;

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
                    // Bind configuration sections
                    services.Configure<SharePointOptions>(
                        context.Configuration.GetSection("SharePoint")
                    );
                    services.Configure<TrackingOptions>(
                        context.Configuration.GetSection("Tracking")
                    );

                    // Register application services
                    services.AddSingleton<IAuthContextFactory, AuthContextFactory>();
                    services.AddSingleton<IFolderProcessor, FolderProcessor>();
                    services.AddSingleton<SharePointService>();

                    // Worker that invokes SharePointService on a timer
                    services.AddHostedService<Worker>();
                });

            // If deploying as a Windows Service, uncomment:
            // builder.UseWindowsService();

            var host = builder.Build();
            await host.RunAsync();
        }
    }
}
