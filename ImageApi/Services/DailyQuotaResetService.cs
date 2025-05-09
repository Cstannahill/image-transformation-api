using ImageApi.Helpers;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ImageApi.Services
{
    public class DailyQuotaResetService : IHostedService, IDisposable
    {
        private Timer? _timer;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            ScheduleNextReset();
            return Task.CompletedTask;
        }

        private void ScheduleNextReset()
        {
            // Compute time until next local midnight
            var now = DateTime.Now;
            var nextMidnight = now.Date.AddDays(1);
            var delay = nextMidnight - now;

            _timer = new Timer(_ =>
            {
                DailyQuotaStore.ResetAll();
                ScheduleNextReset();  // re-schedule for the following midnight
            },
            null,
            delay,
            Timeout.InfiniteTimeSpan);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose() => _timer?.Dispose();
    }
}
