using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;               // ← add this
using SharePointMirror.Options;                   // ← and this

namespace SharePointMirror
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly SharePointService _spService;
        private readonly TrackingOptions _track;   // now resolved

        public Worker(
            ILogger<Worker> logger,
            SharePointService spService,
            IOptions<TrackingOptions> trackOptions)  // inject IOptions<TrackingOptions>
        {
            _logger    = logger;
            _spService = spService;
            _track     = trackOptions.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker started at: {time}", DateTimeOffset.Now);
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug("Worker running at: {time}", DateTimeOffset.Now);

                await _spService.ProcessAsync(stoppingToken);

                await Task.Delay(
                    TimeSpan.FromSeconds(_track.PollIntervalSeconds),
                    stoppingToken
                );
            }
            _logger.LogInformation("Worker stopped at: {time}", DateTimeOffset.Now);
        }
    }
}
