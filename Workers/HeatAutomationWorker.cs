
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
                    _heatAutomationWorkerProvider.SetHeatBasedOnPriceTask = SetHeatBasedOnPrice();
                    await _heatAutomationWorkerProvider.SetHeatBasedOnPriceTask;
                    _logger.LogWarning($"{_serviceName}:: ExecuteAsync tasks completed unexpectedly, restarting...");
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
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                    continue;
                }
                var latestOuman = (int)Math.Round(_oumanProvider.LatestFlowDemand, 0, MidpointRounding.AwayFromZero);
                if (_heishaMonProvider.MainTargetTemp != latestOuman)
                {
                    _logger.LogInformation($"{_serviceName}:: Syncing Heishamon target temp {_heishaMonProvider.MainTargetTemp} to Ouman flow demand {_oumanProvider.LatestFlowDemand}");
                    var newTarget = latestOuman + _heatAddition;
                    await _heishaMonProvider.SetTargetTemperature(newTarget);
                }
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
        private async Task SetHeatBasedOnPrice()
        {
            if (_priceProvider.TodayLowPriceTimes.Count > 0)
            {
                _logger.LogInformation($"{_serviceName}:: Good");
            }
        }
    }
}
