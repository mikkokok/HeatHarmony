using HeatHarmony.Utils;
using HeatHarmony.Providers;
using HeatHarmony.Config;
using HeatHarmony.Models;

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
            _logger.LogInformation("{service}:: Initialized successfully", _serviceName);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var cycleId = Guid.NewGuid();
                var scope = new
                {
                    worker_cycleId = cycleId,
                    worker_startUtc = DateTime.UtcNow
                };
                using (_logger.BeginScope(scope))
                {
                    try
                    {
                        _heatAutomationWorkerProvider.IsWorkerRunning = true;
                        _logger.LogInformation("{service}:: Running (cycle {cycleId})", _serviceName, cycleId);

                        _heatAutomationWorkerProvider.OumanAndHeishamonSyncTask = SyncOumanAndHeishamon(stoppingToken);
                        _heatAutomationWorkerProvider.SetUseWaterBasedOnPriceTask = SetUseWaterControlBasedOnPrice(stoppingToken);
                        _heatAutomationWorkerProvider.SetInsideTempBasedOnPriceTask = SetInsideTempBasedOnPrice(stoppingToken);

                        var finished = await Task.WhenAny(
                            _heatAutomationWorkerProvider.OumanAndHeishamonSyncTask,
                            _heatAutomationWorkerProvider.SetUseWaterBasedOnPriceTask,
                            _heatAutomationWorkerProvider.SetInsideTempBasedOnPriceTask
                        );

                        _logger.LogWarning("{service}:: One or more tasks completed unexpectedly (cycle {cycleId})", _serviceName, cycleId);

                        if (finished.IsFaulted)
                        {
                            _logger.LogError(finished.Exception, "{service}:: A task faulted. Restarting all loops in 30s unless cancellation requested (cycle {cycleId})", _serviceName, cycleId);
                        }
                        else if (finished.IsCompleted && !stoppingToken.IsCancellationRequested)
                        {
                            _logger.LogWarning("{service}:: A loop exited unexpectedly without fault. Restarting in 30s (cycle {cycleId})", _serviceName, cycleId);
                        }

                        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("{service}:: ExecuteAsync cancelled (cycle {cycleId})", _serviceName, cycleId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "{service}:: ExecuteAsync failed, restarting in 30 seconds... (cycle {cycleId})", _serviceName, cycleId);
                    }
                    finally
                    {
                        _heatAutomationWorkerProvider.IsWorkerRunning = false;
                    }
                }
            }
            _logger.LogInformation("{service}:: Stopped running at {stopped}", _serviceName, DateTime.Now);
            _heatAutomationWorkerProvider.IsWorkerRunning = false;
        }

        private async Task SyncOumanAndHeishamon(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var scope = new
                    {
                        sync_cycleUtc = DateTime.UtcNow,
                        sync_targetTemp = _heishaMonProvider.MainTargetTemp,
                        sync_flowDemand = _oumanProvider.LatestFlowDemand
                    };
                    using (_logger.BeginScope(scope))
                    {
                        if (_heishaMonProvider.MainTargetTemp == 0 || _oumanProvider.LatestFlowDemand == 0.0)
                        {
                            if (DateTime.UtcNow - _startTime > TimeSpan.FromMinutes(GlobalConfig.HeatAutomationConfig.MaxInitializationMinutes))
                            {
                                _logger.LogError("{service}:: Providers failed to initialize within {minutes} minutes", _serviceName, GlobalConfig.HeatAutomationConfig.MaxInitializationMinutes);
                                throw new InvalidOperationException("Provider initialization timeout");
                            }

                            _logger.LogWarning("{service}:: Waiting for provider initialization...", _serviceName);
                            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                            continue;
                        }

                        var latestOuman = (int)Math.Round(_oumanProvider.LatestFlowDemand, 0, MidpointRounding.AwayFromZero);
                        var latestOumanWithHeishaAdjustment = latestOuman + GlobalConfig.HeatAutomationConfig.HeatAddition;
                        var tolerance = 1;

                        if (Math.Abs(_heishaMonProvider.MainTargetTemp - latestOumanWithHeishaAdjustment) > tolerance)
                        {
                            var newTarget = Math.Clamp(latestOumanWithHeishaAdjustment, 20, 65);
                            _logger.LogInformation("{service}:: Adjusting target from {old} to {new}", _serviceName, _heishaMonProvider.MainTargetTemp, newTarget);
                            await _heishaMonProvider.SetTargetTemperature(newTarget);
                        }

                        _logger.LogInformation("{service}:: Sync status - HeishaMon: {heisha}C, Ouman: {ouman}C, Adjustment: {adj}C",
                            _serviceName,
                            _heishaMonProvider.MainTargetTemp,
                            _oumanProvider.LatestFlowDemand,
                            GlobalConfig.HeatAutomationConfig.HeatAddition);

                        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{service}:: Error in SyncOumanAndHeishamon", _serviceName);
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
            }
        }

        private async Task SetUseWaterControlBasedOnPrice(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            DateTime lastDisableAttempt = DateTime.MinValue;
            var rng = new Random();

            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                bool isStale = IsPriceDataStale();
                bool isRunning = await _emProvider.IsRunning();
                bool isOverridden = _emProvider.IsOverridden;
                bool hasRunEnough = !isStale && _emProvider.HasRunEnough();
                bool shouldEnable = !isStale && ShouldEnableWaterHeating();

                var scope = new
                {
                    water_cycleUtc = DateTime.UtcNow,
                    water_lastEnabled = _emProvider.LastEnabled,
                    water_isRunning = isRunning,
                    water_isOverridden = isOverridden,
                    water_isPriceStale = isStale,
                    water_hasRunEnough = hasRunEnough,
                    water_shouldEnable = shouldEnable
                };

                using (_logger.BeginScope(scope))
                {
                    try
                    {
                        if (isOverridden)
                        {
                            _logger.LogInformation("{service}:: Water heating in override mode, skipping automatic control", _serviceName);
                        }
                        else if (isStale)
                        {
                            _logger.LogCritical("{service}:: Price data stale. Fallback strategy engaged", _serviceName);

                            if (TimeUtils.HasBeenLongerThan(_emProvider.LastEnabled, 24) && !isRunning)
                            {
                                if (now.Hour == 0)
                                {
                                    await SafeEnableWaterHeating();
                                }
                                else
                                {
                                    _logger.LogWarning("{service}:: >24h since last run; waiting for midnight to enable", _serviceName);
                                }
                            }
                        }
                        else
                        {
                            if (shouldEnable && !isRunning)
                            {
                                await SafeEnableWaterHeating();
                            }
                            else if (isRunning)
                            {
                                if (hasRunEnough || (!shouldEnable && !IsEmergencyHeatingNeeded()))
                                {
                                    if ((now - lastDisableAttempt) >= TimeSpan.FromMinutes(10))
                                    {
                                        await SafeDisableWaterHeating();
                                        lastDisableAttempt = now;
                                        await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
                                    }
                                    else
                                    {
                                        _logger.LogInformation("{service}:: Skipping disable due to debounce window", _serviceName);
                                    }
                                }
                                else
                                {
                                    _logger.LogInformation("{service}:: Continuing water heating cycle", _serviceName);
                                }
                            }
                            else
                            {
                                _logger.LogInformation("{service}:: Water heating remains off (decision: not optimal)", _serviceName);
                            }
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        var backoffMs = 1000 + rng.Next(0, 500);
                        _logger.LogWarning(ex, "{service}:: Network issue in SetUseWaterControlBasedOnPrice. Backing off {ms}ms", _serviceName, backoffMs);
                        await Task.Delay(TimeSpan.FromMilliseconds(backoffMs), stoppingToken);
                    }
                    catch (TaskCanceledException ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogWarning(ex, "{service}:: Request timeout in SetUseWaterControlBasedOnPrice. Retrying shortly", _serviceName);
                        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "{service}:: Error in SetUseWaterControlBasedOnPrice", _serviceName);
                    }

                    var baseDelay = TimeSpan.FromMinutes(10);
                    var jitter = TimeSpan.FromSeconds(rng.Next(0, 20));
                    await Task.Delay(baseDelay + jitter, stoppingToken);
                }
            }

            _logger.LogInformation("{service}:: SetUseWaterControlBasedOnPrice stopped; cancellation={cancelled}", _serviceName, stoppingToken.IsCancellationRequested);
        }

        private async Task SetInsideTempBasedOnPrice(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                var scope = new
                {
                    inside_cycleUtc = DateTime.UtcNow,
                    inside_outsideTemp = _oumanProvider.LatestOutsideTemp,
                    inside_insideTemp = _oumanProvider.LatestInsideTemp
                };
                using (_logger.BeginScope(scope))
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
                            _logger.LogWarning("{service}:: No valid price data for today, maintaining default inside temp of 20°C", _serviceName);
                            await _oumanProvider.SetConservativeHeating();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "{service}:: Error in SetInsideTempBasedOnPrice", _serviceName);
                    }
                    await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
                }
            }
        }

        private bool ShouldEnableWaterHeating()
        {
            var todayPeriods = _priceProvider.AllLowPriceTimes;
            var bestPricePeriod = todayPeriods.FirstOrDefault(tlp => tlp.Rank == 1 && TimeUtils.IsTimeToday(tlp.Start));
            bool isRankDataValid = bestPricePeriod != null;
            double hoursSinceLast = TimeUtils.HoursSince(_emProvider.LastEnabled);
            var currentPrice = TimeUtils.GetCurrentTimePrice(todayPeriods);

            var scope = new
            {
                decision_bestPeriodRank = bestPricePeriod?.Rank,
                decision_bestPeriodStart = bestPricePeriod?.Start,
                decision_validPriceData = isRankDataValid,
                decision_hoursSinceLastRun = hoursSinceLast,
                decision_currentPrice = currentPrice
            };

            using (_logger.BeginScope(scope))
            {
                _logger.LogInformation("{service}:: Evaluating water heating enablement. Best period: {start}-{end} (Rank {rank}), Valid={valid}, LastEnabled={last}",
                    _serviceName,
                    bestPricePeriod?.Start.ToString("HH:mm"),
                    bestPricePeriod?.End.ToString("HH:mm"),
                    bestPricePeriod?.Rank,
                    isRankDataValid,
                    _emProvider.LastEnabled);

                if (!isRankDataValid)
                {
                    if (hoursSinceLast >= 24)
                    {
                        _logger.LogWarning("{service}:: No price data and >24h since last run. Midnight fallback check.", _serviceName);
                        if (DateTime.Now.Hour == 0)
                            return true;
                    }
                    return false;
                }

                if (currentPrice.HasValue && currentPrice.Value <= GlobalConfig.HeatAutomationConfig.CheapPriceThreshold)
                {
                    _logger.LogInformation("{service}:: Current price {price} <= cheap threshold -> enable", _serviceName, currentPrice);
                    return true;
                }

                if (bestPricePeriod != null && TimeUtils.IsCurrentTimeInRange(bestPricePeriod) && hoursSinceLast > 3)
                {
                    _logger.LogInformation("{service}:: In best price window and sufficient hours since last run -> enable", _serviceName);
                    return true;
                }

                if (IsEmergencyHeatingNeeded())
                {
                    _logger.LogWarning("{service}:: Emergency condition (>48h) -> enable", _serviceName);
                    return true;
                }

                _logger.LogInformation("{service}:: Not enabling water heating this cycle", _serviceName);
                return false;
            }
        }

        private async Task ControlInsideTemp()
        {
            var bestPricePeriod = _priceProvider.AllLowPriceTimes.FirstOrDefault(tlp => tlp.Rank == 1 && TimeUtils.IsTimeToday(tlp.Start));
            bool isRankDataValid = bestPricePeriod != null;
            int mintemp = 20;
            int midtemp = 40;
            int maxtemp = 55;
            var outside = _oumanProvider.LatestOutsideTemp;
            var inside = _oumanProvider.LatestInsideTemp;
            var nightPeriod = _priceProvider.NightPeriodTimes;
            var nightHours = TimeUtils.GetHoursInRange(nightPeriod);
            var bestHours = bestPricePeriod != null ? TimeUtils.GetHoursInRange(bestPricePeriod) : 0;

            var scope = new
            {
                inside_validPriceData = isRankDataValid,
                inside_bestPeriodRank = bestPricePeriod?.Rank,
                inside_bestPeriodHours = bestHours,
                inside_nightPeriodHours = nightHours,
                inside_outsideTemp = outside,
                inside_insideTemp = inside
            };

            using (_logger.BeginScope(scope))
            {
                if (inside > 26)
                {
                    _logger.LogInformation("{service}:: Inside temp {insideTemp}C high -> conservative heating", _serviceName, inside);
                    await _oumanProvider.SetConservativeHeating();
                    await SetTRVAuto();
                    return;
                }

                if (!isRankDataValid)
                {
                    if (DateTime.Now.Hour is > 0 and < 6 && outside < 15 && outside > -5)
                    {
                        _logger.LogWarning("{service}:: No price data, early hours moderate outside -> opportunistic high flow", _serviceName);
                        await SetOumanAutoAndMin(maxtemp);
                        await SetTRVMaxHeating();
                        return;
                    }
                    _logger.LogWarning("{service}:: No price data -> conservative heating", _serviceName);
                    await _oumanProvider.SetConservativeHeating();
                    await SetTRVAuto();
                    return;
                }

                if (outside >= 15)
                {
                    if (TimeUtils.IsCurrentTimeInRange(nightPeriod))
                    {
                        _logger.LogInformation("{service}:: Summer + night window (hours={hours})", _serviceName, nightHours);
                        if (nightHours > 8)
                        {
                            await SetOumanAutoAndMin(midtemp);
                            await SetTRVAuto();
                        }
                        else
                        {
                            await SetOumanAutoAndMin(maxtemp);
                            await SetTRVMaxHeating();
                        }
                    }
                    else
                    {
                        _logger.LogInformation("{service}:: Summer outside night window -> minimal flow", _serviceName);
                        await SetOumanAutoAndMin(mintemp);
                        await SetTRVAuto();
                    }
                    return;
                }

                if (outside > -5)
                {
                    if (bestHours > 16 && TimeUtils.IsCurrentTimeInRange(bestPricePeriod))
                    {
                        _logger.LogInformation("{service}:: Shoulder long cheap window ({hours}h) -> mid flow", _serviceName, bestHours);
                        await SetOumanAutoAndMin(40);
                        await SetTRVAuto();
                    }
                    else if (TimeUtils.IsCurrentTimeInRange(nightPeriod))
                    {
                        _logger.LogInformation("{service}:: Shoulder night window -> max flow", _serviceName);
                        await SetOumanAutoAndMin(55);
                        await SetTRVMaxHeating();
                    }
                    else
                    {
                        _logger.LogInformation("{service}:: Shoulder outside cheap/night -> low flow", _serviceName);
                        await SetOumanAutoAndMin(20);
                        await SetTRVAuto();
                    }
                    return;
                }
                else if (outside <= -5)
                {
                    var currentPeriod = _priceProvider.AllLowPriceTimes.FirstOrDefault(p => TimeUtils.IsCurrentTimeInRange(p));
                    if (currentPeriod == null)
                    {
                        _logger.LogInformation("{service}:: Winter no current period -> default", _serviceName);
                        await _oumanProvider.SetDefault();
                        await SetTRVAuto();
                        return;
                    }

                    var winterScope = new
                    {
                        winter_currentRank = currentPeriod.Rank,
                        winter_currentAvgPrice = currentPeriod.AveragePrice
                    };
                    using (_logger.BeginScope(winterScope))
                    {
                        if (currentPeriod.AveragePrice > GlobalConfig.HeatAutomationConfig.ExpensivePriceThreshold)
                        {
                            _logger.LogInformation("{service}:: Winter expensive -> inside 19C", _serviceName);
                            await SetOumanAutoAndInside(19);
                            await SetTRVAuto();
                            return;
                        }
                        if (currentPeriod.Rank == 1)
                        {
                            _logger.LogInformation("{service}:: Winter cheapest -> inside 22C + TRV max", _serviceName);
                            await SetOumanAutoAndInside(22);
                            await SetTRVMaxHeating();
                            return;
                        }
                        _logger.LogInformation("{service}:: Winter cheap rank {rank} -> inside 20C", _serviceName, currentPeriod.Rank);
                        await SetOumanAutoAndInside(20);
                        await SetTRVAuto();
                        return;
                    }
                }
                else
                {
                    _logger.LogInformation("{service}:: Extreme cold branch -> inside 19C", _serviceName);
                    await SetOumanAutoAndInside(19);
                    await SetTRVAuto();
                }
                _logger.LogInformation("{service}:: Fallback conservative heating", _serviceName);
                await _oumanProvider.SetConservativeHeating();
                await SetTRVAuto();
            }
        }

        private bool IsEmergencyHeatingNeeded() => TimeUtils.HoursSince(_emProvider.LastEnabled) >= 48;

        private bool IsPriceDataStale()
        {
            if (_priceProvider.AllLowPriceTimes.Count == 0)
            {
                _logger.LogWarning("{service}:: No low price times available", _serviceName);
                return true;
            }
            var now = DateTime.Now;
            bool anyToday = _priceProvider.AllLowPriceTimes.Any(p => TimeUtils.IsTimeToday(p.Start));
            if (!anyToday && now.Hour >= 2)
                return true;
            return false;
        }

        private async Task SetOumanAutoAndMin(int newMinTemp)
        {
            _logger.LogDebug("{service}:: SetOumanAutoAndMin({min})", _serviceName, newMinTemp);
            await _oumanProvider.SetAutoDriveOn();
            await _oumanProvider.SetMinFlowTemp(newMinTemp);
        }

        private async Task SetOumanAutoAndInside(int newInsideTemp)
        {
            _logger.LogDebug("{service}:: SetOumanAutoAndInside({inside})", _serviceName, newInsideTemp);
            await _oumanProvider.SetMinFlowTemp(20);
            await _oumanProvider.SetAutoDriveOn();
            await _oumanProvider.SetInsideTemp(newInsideTemp);
        }

        private async Task SetTRVAuto()
        {
            _logger.LogDebug("{service}:: SetTRVAuto()", _serviceName);
            await _tRVProvider.SetAutoTemp(true, null);
        }

        private async Task SetTRVMaxHeating()
        {
            _logger.LogDebug("{service}:: SetTRVMaxHeating()", _serviceName);
            await _tRVProvider.SetHeating(100);
        }

        private async Task SafeEnableWaterHeating()
        {
            _logger.LogInformation("{service}:: Enabling water heating", _serviceName);
            await _emProvider.EnableWaterHeating();
        }

        private async Task SafeDisableWaterHeating()
        {
            _logger.LogInformation("{service}:: Disabling water heating", _serviceName);
            await _emProvider.DisableWaterHeating();
        }
    }
}