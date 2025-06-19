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
            var baseDelay = TimeSpan.FromSeconds(2);
            var maxDelay = TimeSpan.FromMinutes(16);
            int attempt = 0;

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug("Worker running at: {time}", DateTimeOffset.Now);

                try
                {
                    await _spService.ProcessAsync(stoppingToken);
                    attempt = 0; // Reset on success

                    await Task.Delay(
                        TimeSpan.FromSeconds(_track.PollIntervalSeconds),
                        stoppingToken
                    );
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    attempt++;
                    var delay = TimeSpan.FromMilliseconds(
                        Math.Min(
                            baseDelay.TotalMilliseconds * Math.Pow(2, attempt),
                            maxDelay.TotalMilliseconds
                        )
                    );

                    _logger.LogWarning(ex, "Error in ProcessAsync, backing off for {Delay} (attempt {Attempt})", delay, attempt);

                    try
                    {
                        await Task.Delay(delay, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
            _logger.LogInformation("Worker stopped at: {time}", DateTimeOffset.Now);
        }
    }
}
