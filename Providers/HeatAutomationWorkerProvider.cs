using HeatHarmony.Models;
using HeatHarmony.Workers;

namespace HeatHarmony.Providers
{
    public sealed class HeatAutomationWorkerProvider(ILogger<HeatAutomationWorkerProvider> logger, OumanProvider oumanProvider)
    {
        private readonly string _serviceName = nameof(HeatAutomationWorkerProvider);
        private readonly object _sync = new();

        public bool overRide = false;
        public double overRideTemp = 20;
        public DateTime? overRideUntil = null;

        private Task? _overRideTask;
        private CancellationTokenSource _overRideCancellationTokenSource = new();

        public bool IsWorkerRunning { get; set; }
        public Task? OumanAndHeishamonSyncTask { get; set; }
        public Task? SetUseWaterBasedOnPriceTask { get; set; }
        public Task? SetInsideTempBasedOnPriceTask { get; set; }

        public void OverRideTemp(int hours, double temp, bool overRidePrevious, int delay = 0)
        {
            lock (_sync)
            {
                if (overRide && !overRidePrevious)
                {
                    logger.LogInformation("{service}:: Override already in place, ignoring new request", _serviceName);
                    return;
                }

                if (overRidePrevious && _overRideTask is not null)
                {
                    CancelTokenNoThrow();
                    logger.LogInformation("{service}:: Previous override cancelled, starting new one", _serviceName);
                }

                _overRideCancellationTokenSource = new CancellationTokenSource();
                _overRideTask = OverRideTask(delay, hours, temp, _overRideCancellationTokenSource.Token);
            }
        }

        private async Task OverRideTask(int delay, int hours, double temp, CancellationToken ct)
        {
            var operationId = Guid.NewGuid();
            try
            {
                logger.LogInformation("{service}:: [{op}] Override requested: temp {temp} for {hours}h, delay {delay}h",
                    _serviceName, operationId, temp, hours, delay);

                if (delay > 0)
                {
                    await Task.Delay(TimeSpan.FromHours(delay), ct);
                }

                var previousTemp = oumanProvider.LatestInsideTempDemand;

                await oumanProvider.SetInsideTemp(temp);
                lock (_sync)
                {
                    overRide = true;
                    overRideTemp = temp;
                    overRideUntil = DateTime.Now.AddHours(hours);
                }

                logger.LogInformation("{service}:: [{op}] Override applied: temp {temp} until {until}",
                    _serviceName, operationId, temp, overRideUntil);

                await Task.Delay(TimeSpan.FromHours(hours), ct);

                await oumanProvider.SetInsideTemp(previousTemp);
                logger.LogInformation("{service}:: [{op}] Override ended, restored to {prev}", _serviceName, operationId, previousTemp);
            }
            catch (TaskCanceledException)
            {
                try
                {
                    var restoreTo = oumanProvider.LatestInsideTempDemand;
                    logger.LogInformation("{service}:: [{op}] Override task cancelled, restoring temp to {restoreTo}",
                        _serviceName, operationId, restoreTo);
                    await oumanProvider.SetInsideTemp(restoreTo);
                }
                catch (Exception exRestore)
                {
                    logger.LogWarning(exRestore, "{service}:: [{op}] Restoration failed after cancellation", _serviceName, operationId);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{service}:: [{op}] Override task failed", _serviceName, operationId);
            }
            finally
            {
                lock (_sync)
                {
                    overRide = false;
                    overRideUntil = null;
                }
            }
        }

        public void CancelOverRide()
        {
            lock (_sync)
            {
                if (!overRide && _overRideTask is null)
                {
                    logger.LogInformation("{service}:: No override in place to cancel", _serviceName);
                    return;
                }

                CancelTokenNoThrow();
                logger.LogInformation("{service}:: Override cancellation requested", _serviceName);
            }
        }

        private void CancelTokenNoThrow()
        {
            try
            {
                _overRideCancellationTokenSource.Cancel();
            }
            catch (Exception ex) { 
                logger.LogWarning(ex, "{service}:: Exception occurred while cancelling override token", _serviceName);
            }
            finally
            {
                _overRideCancellationTokenSource.Dispose();
            }
        }
    }
}
