
using System.Globalization;
using HeatHarmony.Config;
using HeatHarmony.MQ;
using HeatHarmony.Models;
using HeatHarmony.Providers;

namespace HeatHarmony.Workers
{
    public class ElectricWorker : BackgroundService
    {
        private readonly string _serviceName = nameof(ElectricWorker);
        private readonly ILogger<ElectricWorker> _logger;
        private readonly Pro3Provider _pro3Provider;
        private readonly MQClient _mQClient;
        private readonly PriceProvider _priceProvider;
        private const decimal IdealPricePerKwh = 0.02m;
        private const decimal TransferFeePerKwh = 0.05m;
        private const double Phase2PowerKw = 4.0;
        private const double Phase3PowerKw = 6.0;

        public ElectricWorker(ILogger<ElectricWorker> logger, Pro3Provider pro3Provider, MQClient mQClient, PriceProvider priceProvider)
        {
            _logger = logger;
            _pro3Provider = pro3Provider;
            _mQClient = mQClient;
            _priceProvider = priceProvider;
            _logger.LogInformation("{ServiceName}:: Starting", _serviceName);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("{ServiceName}:: Started", _serviceName);

            int currentOutputPhases = 0;
            var lastSwitchTime = DateTime.MinValue;
            var minSwitchInterval = TimeSpan.FromMinutes(15);

            while (!stoppingToken.IsCancellationRequested)
            {
                var cycleId = Guid.NewGuid();
                var scope = new
                {
                    electric_cycleId = cycleId,
                    electric_cycleTime = DateTime.Now,
                    electric_mqStatus = _mQClient.Status,
                    electric_actualConsumption = _mQClient.ActualConsumption,
                    electric_actualReturndelivery = _mQClient.ActualReturndelivery
                };

                using (_logger.BeginScope(scope))
                {
                    try
                    {
                        currentOutputPhases = _pro3Provider.GetCurrentOutputAmount();
                        if (_mQClient.Status != MQStatusEnum.Connected)
                        {
                            _logger.LogWarning("{service}:: MQ client not connected (status: {status}), skipping control logic (cycle {cycleId})", _serviceName, _mQClient.Status, cycleId);
                            if (currentOutputPhases > 0)
                            {
                                _logger.LogWarning("{service}:: MQ client not connected, turning electric load off (cycle {cycleId})", _serviceName, cycleId);
                                await _pro3Provider.SetOutput(3, false);
                                currentOutputPhases = 0;
                                lastSwitchTime = DateTime.Now;
                            }

                            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
                            continue;
                        }

                        var currentPrice = GetCurrentElectricityPrice();
                        if (currentPrice is null)
                        {
                            _logger.LogWarning("{service}:: No current price available, skipping control logic (cycle {cycleId})", _serviceName, cycleId);
                            if (currentOutputPhases > 0)
                            {
                                _logger.LogWarning("{service}:: No current price available, turning electric load off (cycle {cycleId})", _serviceName, cycleId);
                                await _pro3Provider.SetOutput(3, false);
                                lastSwitchTime = DateTime.Now;
                            }

                            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                            continue;
                        }

                        if (_pro3Provider.IsOverridden)
                        {
                            _logger.LogInformation("{service}:: Pro3 override active until {until}, skipping automatic control (cycle {cycleId})", _serviceName, _pro3Provider.OverrideUntil, cycleId);
                            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                            continue;
                        }

                        var desiredOutputPhases = CalculateDesiredOutputPhases(currentPrice.Value, currentOutputPhases);

                        if (desiredOutputPhases != currentOutputPhases)
                        {
                            var now = DateTime.Now;
                            var timeSinceLastSwitch = now - lastSwitchTime;

                            if (timeSinceLastSwitch < minSwitchInterval && desiredOutputPhases > 0 && currentOutputPhases > 0)
                            {
                                _logger.LogDebug("{ServiceName}:: Skipping change from {Current} -> {Desired} due to debounce ({Seconds:F0}s since last switch)",
                                    _serviceName, currentOutputPhases, desiredOutputPhases, timeSinceLastSwitch.TotalSeconds);
                            }
                            else
                            {
                                _logger.LogInformation("{ServiceName}:: Changing Pro3 output from {Current} -> {Desired} phases at price {Price:F4} €/kWh (cycle {CycleId})",
                                    _serviceName, currentOutputPhases, desiredOutputPhases, currentPrice, cycleId);

                                if (currentOutputPhases > 0)
                                {
                                    await _pro3Provider.SetOutput(currentOutputPhases, false);
                                }

                                if (desiredOutputPhases > 0)
                                {
                                    await _pro3Provider.SetOutput(desiredOutputPhases, true);
                                }

                                lastSwitchTime = now;
                            }
                        }
                        else
                        {
                            _logger.LogDebug("{service}:: Current output phases {phases} already match desired {desiredPhases} at price {price:F4} €/kWh (cycle {cycleId}), no change needed", _serviceName, currentOutputPhases, desiredOutputPhases, currentPrice, cycleId);
                        }

                        await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("{service}:: ExecuteAsync cancelled (cycle {cycleId})", _serviceName, cycleId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "{service}:: Error in ElectricWorker loop (cycle {cycleId})", _serviceName, cycleId);
                        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    }
                }
            }

            if (currentOutputPhases > 0)
            {
                try
                {
                    _logger.LogInformation("{service}:: Stopping worker, turning off Pro3 output ({phases} phases)", _serviceName, currentOutputPhases);
                    await _pro3Provider.SetOutput(currentOutputPhases, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{service}:: Failed to turn off Pro3 output during shutdown", _serviceName);
                }
            }

            _logger.LogInformation("{service}:: Stopped", _serviceName);
        }

        private int CalculateDesiredOutputPhases(decimal currentPrice, int currentOutputPhases)
        {
            if (currentPrice + TransferFeePerKwh <= 0)
            {
                _logger.LogDebug("{service}:: Price {price:F4} €/kWh at or below transfer fee {transfer:F4}, use maximum output", _serviceName, currentPrice, TransferFeePerKwh);
                return 3;
            }

            if (IdealPricePerKwh < currentPrice)
            {
                return 0;
            }

            double MaxGridImportKw = currentPrice switch 
            {
                < 0 => 1.0,
                _ => 0.5
            };

            var exportKw = _mQClient.ActualReturndelivery;
            var importKw = _mQClient.ActualConsumption;

            var currentPro3LoadKw = currentOutputPhases switch
            {
                2 => Phase2PowerKw,
                3 => Phase3PowerKw,
                _ => 0.0
            };

            var realKwBalance = exportKw - importKw + currentPro3LoadKw;
            var surplusKw = Math.Max(realKwBalance, 0.0);

            var availableKw = surplusKw + MaxGridImportKw;

            _logger.LogDebug("{service}:: Price={price:F4}, pro3Load={pro3:F1}kW, realBalance={realBalance:F2}kW, import={import:F2}kW, surplusExport={export:F2}kW, available={available:F2}kW",
                _serviceName, currentPrice, currentPro3LoadKw, realKwBalance, importKw, exportKw, availableKw);

            if (availableKw >= Phase3PowerKw)
            {
                return 3;
            }

            if (availableKw >= Phase2PowerKw)
            {
                return 2;
            }
            return 0;
        }

        private decimal? GetCurrentElectricityPrice()
        {
            var allPrices = new List<ElectricityPrice>();
            allPrices.AddRange(_priceProvider.TodayPrices);
            allPrices.AddRange(_priceProvider.TomorrowPrices);

            if (allPrices.Count == 0)
            {
                _logger.LogWarning("{service}:: No price data available", _serviceName);
                return null;
            }

            var now = DateTime.Now;
            DateTime? bestSlotStart = null;
            decimal? bestPrice = null;

            foreach (var slot in allPrices)
            {
                try
                {
                    var slotStart = DateTime.ParseExact(slot.date, GlobalConst.PriceTimeFormat, CultureInfo.InvariantCulture);
                    var slotPrice = decimal.Parse(slot.price, NumberStyles.Float, CultureInfo.InvariantCulture);

                    var slotEnd = slotStart.AddMinutes(15);

                    if (now >= slotStart && now < slotEnd)
                    {
                        return slotPrice;
                    }

                    if (slotStart <= now && (bestSlotStart is null || slotStart > bestSlotStart.Value))
                    {
                        bestSlotStart = slotStart;
                        bestPrice = slotPrice;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "{service}:: Failed to parse price slot {date} / {price}", _serviceName, slot.date, slot.price);
                }
            }

            return bestPrice;
        }
    }
}
