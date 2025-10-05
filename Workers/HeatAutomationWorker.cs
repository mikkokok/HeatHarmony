using HeatHarmony.Providers;
using System.Formats.Asn1;

namespace HeatHarmony.Workers
{
    public sealed class HeatAutomationWorker : BackgroundService
    {
        private readonly string _serviceName;
        private readonly ILogger<HeatAutomationWorker> _logger;
        private readonly HeishaMonProvider _heishaMonProvider;
        private readonly OumanProvider _oumanProvider;
        private readonly HeatAutomationWorkerProvider _heatAutomationWorkerProvider;
        private readonly PriceProvider _priceProvider;
        private readonly int _heatAddition = 3;
        private readonly DateTime _startTime = DateTime.UtcNow;
        private const int MaxInitializationMinutes = 30;

        public HeatAutomationWorker(ILogger<HeatAutomationWorker> logger, HeishaMonProvider heishaMonProvider,
            OumanProvider oumanProvider, HeatAutomationWorkerProvider heatAutomationWorkerProvider, PriceProvider priceProvider)
        {
            _serviceName = nameof(HeatAutomationWorker);
            _logger = logger;
            _heishaMonProvider = heishaMonProvider;
            _oumanProvider = oumanProvider;
            _heatAutomationWorkerProvider = heatAutomationWorkerProvider;
            _priceProvider = priceProvider;
            _logger.LogInformation($"{_serviceName}:: Initialized successfully");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation($"{_serviceName}:: Running at: {DateTime.Now}");
                    _heatAutomationWorkerProvider.IsWorkerRunning = true;
                    _heatAutomationWorkerProvider.OumanAndHeishamonSyncTask = SyncOumanAndHeishamon(stoppingToken);
                    await _heatAutomationWorkerProvider.OumanAndHeishamonSyncTask;
                    _heatAutomationWorkerProvider.SetHeatBasedOnPriceTask = SetControlBasedOnPrice(stoppingToken);
                    await _heatAutomationWorkerProvider.SetHeatBasedOnPriceTask;
                    _logger.LogWarning($"{_serviceName}:: ExecuteAsync tasks completed unexpectedly, restarting...");
                    await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation($"{_serviceName}:: ExecuteAsync cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"{_serviceName}:: ExecuteAsync failed, restarting in 30 seconds...");
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
                _logger.LogError($"{_serviceName}:: ExecuteAsync failed really unexpectedly, restarting in 30 seconds...");
                _heatAutomationWorkerProvider.IsWorkerRunning = false;
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            _logger.LogInformation($"{_serviceName}:: Stopped running at: {DateTime.Now}");
            _heatAutomationWorkerProvider.IsWorkerRunning = false;
        }

        private async Task SyncOumanAndHeishamon(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_heishaMonProvider.MainTargetTemp == 0 || _oumanProvider.LatestFlowDemand == 0.0)
                {
                    if (DateTime.UtcNow - _startTime > TimeSpan.FromMinutes(MaxInitializationMinutes))
                    {
                        _logger.LogError($"{_serviceName}:: Providers failed to initialize within {MaxInitializationMinutes} minutes");
                        throw new InvalidOperationException("Provider initialization timeout");
                    }

                    _logger.LogWarning($"{_serviceName}:: Waiting for provider initialization...");
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                    continue;
                }
                var latestOuman = (int)Math.Round(_oumanProvider.LatestFlowDemand, 0, MidpointRounding.AwayFromZero);
                var tolerance = 1;

                if (Math.Abs(_heishaMonProvider.MainTargetTemp - latestOuman) > tolerance)
                {
                    var newTarget = Math.Clamp(latestOuman + _heatAddition, 20, 65);
                    _logger.LogInformation($"{_serviceName}:: Adjusting target from {_heishaMonProvider.MainTargetTemp} to {newTarget}");
                    await _heishaMonProvider.SetTargetTemperature(newTarget);
                }
                _logger.LogInformation($"{_serviceName}:: Sync status - HeishaMon: {_heishaMonProvider.MainTargetTemp}°C, Ouman: {_oumanProvider.LatestFlowDemand}°C, Adjustment: {_heatAddition}°C");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
        private async Task SetControlBasedOnPrice(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_priceProvider.TodayLowPriceTimes.Count > 0)
                {
                    _logger.LogInformation($"{_serviceName}:: low price points found for today");

                }
                else
                {
                    _logger.LogCritical($"{_serviceName}:: no low prices have been count for today!, defaulting");
                    await _oumanProvider.SetDefault();
                    while (_priceProvider.TodayLowPriceTimes.Count == 0)
                    {
                        _logger.LogCritical($"{_serviceName}:: no low prices available for today, waiting");
                        await Task.Delay(TimeSpan.FromMinutes(45), stoppingToken);
                    }
                }
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            _logger.LogInformation($"{_serviceName}:: SetHeatBasedOnPrice stopped running at: {DateTime.Now}");
        }
    }
}
