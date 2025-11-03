using HeatHarmony.Utils;
using HeatHarmony.Providers;
using HeatHarmony.Config;

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
        private readonly TRVProvider _tRVProvider;
        private readonly DateTime _startTime = DateTime.UtcNow;

        public HeatAutomationWorker(ILogger<HeatAutomationWorker> logger, HeishaMonProvider heishaMonProvider,
            OumanProvider oumanProvider, HeatAutomationWorkerProvider heatAutomationWorkerProvider, PriceProvider priceProvider, EMProvider eMProvider, TRVProvider tRVProvider)
        {
            _serviceName = nameof(HeatAutomationWorker);
            _logger = logger;
            _heishaMonProvider = heishaMonProvider;
            _oumanProvider = oumanProvider;
            _heatAutomationWorkerProvider = heatAutomationWorkerProvider;
            _priceProvider = priceProvider;
            _emProvider = eMProvider;
            _tRVProvider = tRVProvider;
            _logger.LogInformation($"{_serviceName}:: Initialized successfully");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _heatAutomationWorkerProvider.IsWorkerRunning = true;
            try
            {
                _logger.LogInformation($"{_serviceName}:: Running at: {DateTime.Now}");

                _heatAutomationWorkerProvider.OumanAndHeishamonSyncTask = SyncOumanAndHeishamon(stoppingToken);
                _heatAutomationWorkerProvider.SetUseWaterBasedOnPriceTask = SetUseWaterControlBasedOnPrice(stoppingToken);
                _heatAutomationWorkerProvider.SetInsideTempBasedOnPriceTask = SetInsideTempBasedOnPrice(stoppingToken);

                await Task.WhenAny(
                    _heatAutomationWorkerProvider.OumanAndHeishamonSyncTask,
                    _heatAutomationWorkerProvider.SetUseWaterBasedOnPriceTask,
                    _heatAutomationWorkerProvider.SetInsideTempBasedOnPriceTask
                 );

                _logger.LogWarning($"{_serviceName}:: One or more tasks completed unexpectedly");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation($"{_serviceName}:: ExecuteAsync cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{_serviceName}:: ExecuteAsync failed, restarting in 30 seconds...");
            }
            finally
            {
                _heatAutomationWorkerProvider.IsWorkerRunning = false;
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
                    if (DateTime.UtcNow - _startTime > TimeSpan.FromMinutes(GlobalConfig.HeatAutomationConfig.MaxInitializationMinutes))
                    {
                        _logger.LogError($"{_serviceName}:: Providers failed to initialize within {GlobalConfig.HeatAutomationConfig.MaxInitializationMinutes} minutes");
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
                    var newTarget = Math.Clamp(latestOuman + GlobalConfig.HeatAutomationConfig.HeatAddition, 20, 65);
                    _logger.LogInformation($"{_serviceName}:: Adjusting target from {_heishaMonProvider.MainTargetTemp} to {newTarget}");
                    await _heishaMonProvider.SetTargetTemperature(newTarget);
                }
                _logger.LogInformation($"{_serviceName}:: Sync status - HeishaMon: {_heishaMonProvider.MainTargetTemp}°C, Ouman: {_oumanProvider.LatestFlowDemand}°C, Adjustment: {GlobalConfig.HeatAutomationConfig.HeatAddition}°C");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
        private async Task SetUseWaterControlBasedOnPrice(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (!IsPriceDataStale())
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
                                _logger.LogInformation($"{_serviceName}:: Disabling water heating - Reason: {(hasRunEnough ? "Has run enough" : "No longer optimal time")}");
                                await _emProvider.DisableWaterHeating();
                                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
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

                        if (!TimeUtils.IsTimeWithinHourRange(_emProvider.LastEnabled, 24))
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

        private async Task SetInsideTempBasedOnPrice(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (!IsPriceDataStale())
                    {
                        await ControlInsideTemp();
                        await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
                    }
                    else
                    {
                        _logger.LogWarning($"{_serviceName}:: No valid price data for today, maintaining default inside temp of 20°C");
                        await _oumanProvider.SetConservativeHeating();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"{_serviceName}:: Error in SetInsideTempBasedOnPrice");
                }
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
        }

        private bool ShouldEnableWaterHeating()
        {
            if (_emProvider.IsRunning())
            {
                return false;
            }

            var bestPricePeriod = _priceProvider.TodayLowPriceTimes.FirstOrDefault(tlp => tlp.Rank == 1);
            var isRankDataValid = bestPricePeriod != null && TimeUtils.IsTimeToday(bestPricePeriod.Start);

            _logger.LogInformation($"{_serviceName}:: Evaluating water heating enablement. " +
                $"Best period: {bestPricePeriod?.Start:HH:mm}-{bestPricePeriod?.End:HH:mm} (Rank {bestPricePeriod?.Rank}), " +
                $"Valid data: {isRankDataValid}, Last enabled: {_emProvider.LastEnabled}");

            // No valid price data OR last enabled >24h ago
            if (!isRankDataValid || bestPricePeriod == null)
            {
                if (!TimeUtils.IsTimeWithinHourRange(_emProvider.LastEnabled, 24))
                {
                    _logger.LogInformation($"{_serviceName}:: No valid price data, enabling water heating as fallback");
                    return true;
                }
                return false;
            }

            if (TimeUtils.GetCurrentTimePrice(_priceProvider.TodayLowPriceTimes) <= GlobalConfig.HeatAutomationConfig.CheapPriceThreshold)
            {
                return true;
            }

            // Currently in best price period and hasn't run in last 12 hours
            if (TimeUtils.IsCurrentTimeInRange(bestPricePeriod))
            {
                if (TimeUtils.HowManyHoursUntil(_emProvider.LastEnabled) >= 12)
                {
                    if (bestPricePeriod.AveragePrice <= GlobalConfig.HeatAutomationConfig.CheapPriceThreshold)
                    {
                        _logger.LogInformation($"{_serviceName}:: In good price period (avg: {bestPricePeriod.AveragePrice:F4}) and sufficient time (12hrs) since last run");
                        return true;
                    }
                }
                else if (TimeUtils.HowManyHoursUntil(_emProvider.LastEnabled) >= 24)
                {
                    var priceThreshold = 0.15m; // 15 cents per kWh
                    if (bestPricePeriod.AveragePrice <= priceThreshold)
                    {
                        _logger.LogInformation($"{_serviceName}:: In semi good price period (avg: {bestPricePeriod.AveragePrice:F4}) and sufficient time (24hrs) since last run");
                        return true;
                    }
                }

            }
            // Emergency case - hasn't run in 24+ hours regardless of price
            if (!TimeUtils.IsTimeWithinHourRange(_emProvider.LastEnabled, 48))
            {
                _logger.LogWarning($"{_serviceName}:: Emergency water heating - hasn't run in >48 hours");
                return true;
            }

            _logger.LogInformation($"{_serviceName}:: Water heating not needed at this time");
            return false;
        }

        private async Task ControlInsideTemp()
        {
            var bestPricePeriod = _priceProvider.TodayLowPriceTimes.FirstOrDefault(tlp => tlp.Rank == 1);
            var isRankDataValid = bestPricePeriod != null && TimeUtils.IsTimeToday(bestPricePeriod.Start);
            var mintemp = 20;
            var maxtemp = 55;

            // Set conservative heating if no valid data
            if (bestPricePeriod == null || !isRankDataValid)
            {
                await _oumanProvider.SetConservativeHeating();
                await SetTRVAuto();
                _logger.LogWarning($"{_serviceName}:: No valid price rank data for today, setting conservative heating. Check again in hour");
                return;
            }

            var latestOutsideTemp = _oumanProvider.LatestOutsideTemp;
            var nightPeriod = _priceProvider.GetBestNightPeriod();
            var nightPeriodHours = TimeUtils.GetHoursInRange(nightPeriod);
            var bestPeriodHours = TimeUtils.GetHoursInRange(bestPricePeriod);
            if (latestOutsideTemp >= 15) // summer mode
            {
                if (TimeUtils.IsCurrentTimeInRange(nightPeriod))
                {
                    switch (nightPeriod.AveragePrice)
                    {
                        case <= GlobalConfig.HeatAutomationConfig.CheapPriceThreshold:
                            mintemp = 35;
                            maxtemp = 45;
                            break;
                        case <= GlobalConfig.HeatAutomationConfig.ModeratePriceThreshold:
                            mintemp = 30;
                            maxtemp = 40;
                            break;
                        case > GlobalConfig.HeatAutomationConfig.ModeratePriceThreshold:
                            mintemp = 20;
                            maxtemp = 20;
                            break;
                    }
                    _logger.LogInformation($"{_serviceName}:: Summer mode active with night period ({nightPeriodHours:F1}h), setting MinFlowTemp to {mintemp}°C");
                    if (nightPeriodHours > 8)
                    {
                        await SetOumanAutoAndMin(mintemp);
                        await SetTRVAuto();
                        return;
                    }
                    else
                    {
                        await SetOumanAutoAndMin(maxtemp);
                        await SetTRVMaxHeating();
                        return;
                    }
                }
                else
                {
                    _logger.LogInformation($"{_serviceName}:: Summer mode active but not in cheap period, setting MinFlowTemp to 20°C");
                    await SetOumanAutoAndMin(20);
                    await SetTRVAuto();
                    return;
                }
            }
            else if (latestOutsideTemp > 0 && (TimeUtils.IsCurrentTimeInRange(nightPeriod) || TimeUtils.IsCurrentTimeInRange(bestPricePeriod))) // spring/autumn mode in cheap period
            {
                if (bestPeriodHours > 16 && TimeUtils.IsCurrentTimeInRange(bestPricePeriod))
                {
                    _logger.LogInformation($"{_serviceName}:: Spring/autumn mode active with long cheap period ({bestPeriodHours:F1}h), setting MinFlowTemp to 40");
                    await SetOumanAutoAndMin(40);
                    await SetTRVAuto();
                    return;
                }
                else if (TimeUtils.IsCurrentTimeInRange(nightPeriod))
                {
                    _logger.LogInformation($"{_serviceName}:: Spring/autumn mode active with night period ({nightPeriodHours:F1}h), setting MinFlowTemp");
                    await SetOumanAutoAndMin(55);
                    await SetTRVMaxHeating();
                    return;
                }
            }
            else if (latestOutsideTemp > 0) // spring/autumn mode not in cheap period
            {
                _logger.LogInformation($"{_serviceName}:: Spring/autumn mode active but not in cheap period, setting MinFlowTemp to 20°C");
                await SetOumanAutoAndMin(20);
                await SetTRVAuto();
                return;
            }
            else if (latestOutsideTemp > -10) // winter mode
            {
                var currentPeriod = _priceProvider.TodayLowPriceTimes.FirstOrDefault(p => TimeUtils.IsCurrentTimeInRange(p));
                if (currentPeriod == null)
                {
                    _logger.LogInformation($"{_serviceName}:: Winter mode active, unable to calculate currentPeriod. Set auto.");
                    await _oumanProvider.SetDefault();
                    await SetTRVAuto();
                    return;
                }
                if (currentPeriod.AveragePrice > GlobalConfig.HeatAutomationConfig.ExpensivePriceThreshold)
                {
                    _logger.LogInformation($"{_serviceName}:: Winter mode active with expensive period (Rank {currentPeriod.Rank}, Price {currentPeriod.AveragePrice:F4}), setting inside temp to 19°C");
                    await SetOumanAutoAndInside(19);
                    await SetTRVAuto();
                    return;
                }
                if (currentPeriod.Rank == 1)
                {
                    _logger.LogInformation($"{_serviceName}:: Winter mode active with cheapest period (Rank 1), setting inside temp to 22°C");
                    await SetOumanAutoAndInside(22);
                    await SetTRVMaxHeating();
                    return;
                }
                else
                {
                    _logger.LogInformation($"{_serviceName}:: Winter mode active with cheap period (Rank {currentPeriod.Rank}), setting inside temp to 20°C");
                    await SetOumanAutoAndInside(20);
                    await SetTRVAuto();
                    return;
                }
            }
            else  // extreme cold mode
            {
                _logger.LogInformation($"{_serviceName}:: Extreme cold mode active, setting inside temp to 19°C");
                await SetOumanAutoAndInside(19);
                await SetTRVAuto();
                return;
            }
        }

        private bool IsEmergencyHeatingNeeded()
        {
            return _emProvider.LastEnabled != null && !TimeUtils.IsTimeWithinHourRange(_emProvider.LastEnabled, 48);
        }
        private bool IsPriceDataStale()
        {
            if (_priceProvider.TodayPrices.Count == 0)
            {
                return true;
            }
            return !TimeUtils.IsTimeToday(_priceProvider.TodayLowPriceTimes[0].Start);
        }

        private async Task SetOumanAutoAndMin(int newMinTemp)
        {
            await _oumanProvider.SetAutoDriveOn();
            await _oumanProvider.SetMinFlowTemp(newMinTemp);
        }

        private async Task SetOumanAutoAndInside(int newInsideTemp)
        {
            await _oumanProvider.SetMinFlowTemp(20);
            await _oumanProvider.SetAutoDriveOn();
            await _oumanProvider.SetInsideTemp(newInsideTemp);
        }

        private async Task SetTRVAuto()
        {
            await _tRVProvider.SetAutoTemp(true, null);
        }
        private async Task SetTRVMaxHeating()
        {
            await _tRVProvider.SetHeating(100);
        }
    }
}
