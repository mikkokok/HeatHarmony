using HeatHarmony.Models;
using HeatHarmony.Workers;

namespace HeatHarmony.Providers
{
    public sealed class HeatAutomationWorkerProvider(ILogger<HeatAutomationWorkerProvider> logger, OumanProvider oumanProvider)
    {
        private readonly string _serviceName = nameof(HeatAutomationWorkerProvider);
        public bool overRide = false;
        public int overRideTemp = 20;
        public DateOnly overRideUntil = DateOnly.MinValue;
        private Task? _overRideTask;
        private CancellationTokenSource _overRideCancellationTokenSource = new();
        public bool IsWorkerRunning { get; set; }
        public Task? OumanAndHeishamonSyncTask { get; set; }
        public Task? SetUseWaterBasedOnPriceTask { get; set; }
        public Task? SetInsideTempBasedOnPriceTask { get; set; }
        public void OverRideTemp(int hours, double temp, bool overRidePrevious, int delay = 0)
        {
            if (overRide && !overRidePrevious)
            {
                logger.LogInformation($"{_serviceName}:: Overriding already in place, ignoring new request");
                return;
            }
            if (overRidePrevious && _overRideTask != null)
            {
                _overRideCancellationTokenSource.Cancel();
                _overRideTask.Dispose();
                logger.LogInformation($"{_serviceName}:: Previous override cancelled, starting new one");
                _overRideTask = OverRideTask(delay, hours, temp, _overRideCancellationTokenSource.Token);
            }
            else
            {
                _overRideTask = OverRideTask(delay, hours, temp, _overRideCancellationTokenSource.Token);
            }
        }
        private async Task OverRideTask(int delay, int hours, double temp, CancellationToken ct)
        {
            overRide = true;
            logger.LogInformation($"{_serviceName}:: Overriding temp to {temp} for {hours} hours, delay for {delay}");
            if (delay > 0)
            {
                await Task.Delay(TimeSpan.FromHours(delay), ct);
            }
            var _previousTemp = oumanProvider.LatestInsideTempDemand;
            await oumanProvider.SetInsideTemp(temp);
            overRideUntil = DateOnly.FromDateTime(DateTime.Now.AddHours(hours));
            try
            {
                await Task.Delay(TimeSpan.FromHours(hours), ct);
                await oumanProvider.SetInsideTemp(_previousTemp);
            }
            catch (TaskCanceledException tcex)
            {
                logger.LogInformation($"{_serviceName}:: Override task cancelled: {tcex.Message}");
            }
            overRide = false;
            logger.LogInformation($"{_serviceName}:: Overriding temp ended, set back to {_previousTemp}");
        }

        public void CancelOverRide()
        {
            if (overRide)
            {
                _overRideCancellationTokenSource.Cancel();
                overRide = false;
                logger.LogInformation($"{_serviceName}:: Override cancelled manually");
            }
            else
            {
                logger.LogInformation($"{_serviceName}:: No override in place to cancel");
            }
        }
    }
}
