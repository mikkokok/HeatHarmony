using HeatHarmony.Helpers;
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
        private readonly EMProvider _emProvider;
        private readonly int _heatAddition = 3;
        private readonly DateTime _startTime = DateTime.UtcNow;
        private const int MaxInitializationMinutes = 30;

        public HeatAutomationWorker(ILogger<HeatAutomationWorker> logger, HeishaMonProvider heishaMonProvider,
            OumanProvider oumanProvider, HeatAutomationWorkerProvider heatAutomationWorkerProvider, PriceProvider priceProvider, EMProvider eMProvider)
        {
            _serviceName = nameof(HeatAutomationWorker);
            _logger = logger;
            _heishaMonProvider = heishaMonProvider;
            _oumanProvider = oumanProvider;
            _heatAutomationWorkerProvider = heatAutomationWorkerProvider;
            _priceProvider = priceProvider;
            _emProvider = eMProvider;
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
                try
                {
                    if (_priceProvider.TodayLowPriceTimes.Count > 0 && TimeHelpers.IsTimeToday(_priceProvider.TodayLowPriceTimes[0].Start))
                    {
                        _logger.LogInformation($"{_serviceName}:: low price points found for today");
                        var hasRunEnough = _emProvider.HasRunEnough();
                        var shouldEnable = ShouldEnableWaterHeating();
                        var isCurrentlyRunning = _emProvider.IsRunning();
                        if (shouldEnable && !isCurrentlyRunning && !_emProvider.IsOverridden)
                        {
                            _logger.LogInformation($"{_serviceName}:: Enabling water heating");
                            await _emProvider.EnableWaterHeating();
                        }
                        else if (isCurrentlyRunning && !_emProvider.IsOverridden)
                        {
                            if (hasRunEnough || (!shouldEnable && !IsEmergencyHeatingNeeded()))
                            {
                                _logger.LogInformation($"{_serviceName}:: Disabling water heating - " +
                                    $"Reason: {(hasRunEnough ? "Has run enough" : "No longer optimal time")}");
                                await _emProvider.DisableWaterHeating();
                            }
                            else
                            {
                                _logger.LogInformation($"{_serviceName}:: Continuing water heating cycle");
                            }
                        }
                        else if (_emProvider.IsOverridden)
                        {
                            _logger.LogInformation($"{_serviceName}:: Water heating in override mode, skipping automatic control");
                        }
                    }
                    else
                    {
                        _logger.LogCritical($"{_serviceName}:: No low prices calculated for today! Using fallback strategy");

                        if (_emProvider.LastEnabled == null || !TimeHelpers.IsTimeWithinHourRange(_emProvider.LastEnabled, 24))
                        {
                            if (!_emProvider.IsRunning() && !_emProvider.IsOverridden)
                            {
                                _logger.LogWarning($"{_serviceName}:: Emergency water heating - no price data and >24h since last run. Will wait for midnight to start");
                                if (DateTime.Now.Hour == 0)
                                    await _emProvider.EnableWaterHeating();
                            }
                        }

                        _logger.LogCritical($"{_serviceName}:: Waiting for price data...");
                        await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"{_serviceName}:: Error in SetControlBasedOnPrice");
                }
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);

            }
            _logger.LogInformation($"{_serviceName}:: SetHeatBasedOnPrice stopped running at: {DateTime.Now}, cancellation being {stoppingToken.IsCancellationRequested}");
        }

        private bool ShouldEnableWaterHeating()
        {
            if (_emProvider.IsRunning())
            {
                return false;
            }

            var bestPricePeriod = _priceProvider.TodayLowPriceTimes.FirstOrDefault(tlp => tlp.Rank == 1);
            var isRankDataValid = bestPricePeriod != null && TimeHelpers.IsTimeToday(bestPricePeriod.Start);

            _logger.LogInformation($"{_serviceName}:: Evaluating water heating enablement. " +
                $"Best period: {bestPricePeriod?.Start:HH:mm}-{bestPricePeriod?.End:HH:mm} (Rank {bestPricePeriod?.Rank}), " +
                $"Valid data: {isRankDataValid}, Last enabled: {_emProvider.LastEnabled}");

            // No valid price data and never enabled OR last enabled >24h ago
            if (!isRankDataValid)
            {
                if (_emProvider.LastEnabled == null || !TimeHelpers.IsTimeWithinHourRange(_emProvider.LastEnabled, 24))
                {
                    _logger.LogInformation($"{_serviceName}:: No valid price data, enabling water heating as fallback");
                    return true;
                }
                return false;
            }

            // Never enabled and approaching good price period (within 4 hours)
            if (_emProvider.LastEnabled == null && TimeHelpers.HowManyHoursUntil(bestPricePeriod.Start) <= 4)
            {
                _logger.LogInformation($"{_serviceName}:: Never enabled and good price period approaching in {TimeHelpers.HowManyHoursUntil(bestPricePeriod.Start):F1} hours");
                return true;
            }

            // Currently in best price period and hasn't run in last 12 hours
            if (TimeHelpers.IsCurrentTimeInRange(bestPricePeriod))
            {
                if (_emProvider.LastEnabled == null || TimeHelpers.HowManyHoursUntil(_emProvider.LastEnabled) >= 12)
                {
                    var priceThreshold = 0.05m; // 5 cents per kWh
                    if (bestPricePeriod.AveragePrice <= priceThreshold)
                    {
                        _logger.LogInformation($"{_serviceName}:: In good price period (avg: {bestPricePeriod.AveragePrice:F4}) and sufficient time (12hrs) since last run");
                        return true;
                    }
                }
                else if (_emProvider.LastEnabled == null || TimeHelpers.HowManyHoursUntil(_emProvider.LastEnabled) >= 24)
                {
                    var priceThreshold = 0.15m; // 5 cents per kWh
                    if (bestPricePeriod.AveragePrice <= priceThreshold)
                    {
                        _logger.LogInformation($"{_serviceName}:: In semi good price period (avg: {bestPricePeriod.AveragePrice:F4}) and sufficient time (24hrs) since last run");
                        return true;
                    }
                }

            }
            // Emergency case - hasn't run in 24+ hours regardless of price
            if (_emProvider.LastEnabled != null && !TimeHelpers.IsTimeWithinHourRange(_emProvider.LastEnabled, 48))
            {
                _logger.LogWarning($"{_serviceName}:: Emergency water heating - hasn't run in >48 hours");
                return true;
            }

            _logger.LogInformation($"{_serviceName}:: Water heating not needed at this time");
            return false;
        }

        private bool IsEmergencyHeatingNeeded()
        {
            return _emProvider.LastEnabled != null && !TimeHelpers.IsTimeWithinHourRange(_emProvider.LastEnabled, 48);
        }
    }
}
