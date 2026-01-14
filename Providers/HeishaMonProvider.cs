using HeatHarmony.Config;
using HeatHarmony.Models;
using HeatHarmony.Utils;

namespace HeatHarmony.Providers
{
    public sealed class HeishaMonProvider
    {
        private readonly string _serviceName;
        private readonly ILogger<HeishaMonProvider> _logger;
        private readonly IRequestProvider _requestProvider;
        public Task HeishaMonTask { get; private set; }
        public double MainInletTemp { get; private set; } = 0.0;
        public double MainOutletTemp { get; private set; } = 0.0;
        public int MainTargetTemp { get; private set; } = 0;
        public int QuietMode { get; private set; } = 0;
        public List<HarmonyChange> Changes { get; private set; } = [];
        public HeishaMonProvider(ILogger<HeishaMonProvider> logger, IRequestProvider requestProvider)
        {
            _serviceName = nameof(HeishaMonProvider);
            _logger = logger;
            _requestProvider = requestProvider;
            HeishaMonTask = UpdateHeishaMonStatus();
        }
        public async Task UpdateHeishaMonStatus()
        {
            while (true)
            {
                try
                {
                    var url = GlobalConfig.HeishaUrl + "json";
                    var result = await _requestProvider.GetAsync<HeishaJsonResponse>(HttpClientConst.HeishaClient, url)
                        ?? throw new Exception($"{_serviceName}:: UpdateHeishaMonStatus returned null");
                    foreach (var hinfo in result.heatpump)
                    {
                        HandleHeatPumpInfo(hinfo);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, $"{_serviceName}:: UpdateDeviceStatus failed");
                }
                await Task.Delay(TimeSpan.FromMinutes(5));
            }
        }

        public async Task SetTargetTemperature(int temperature)
        {
            try
            {
                var url = GlobalConfig.HeishaUrl + $"command?SetZ1HeatRequestTemperature={temperature}";
                var result = await _requestProvider.GetStringAsync(HttpClientConst.HeishaClient, url)
                    ?? throw new Exception($"{_serviceName}:: SetTargetTemperature returned null");
                MainTargetTemp = int.Parse(result.Split(" ")[6]);
                LogUtils.AddChangeRecord(Changes, Provider.HeishaMon, HarmonyChangeType.SetTargetTemp, $"New temp {temperature}");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, $"{_serviceName}:: SetTargetTemperature failed");
            }
        }

        public async Task SetQuietMode(int mode)
        {
            if (QuietMode == mode)
            {
                _logger.LogInformation($"{_serviceName}:: SetQuietMode called but mode is already {mode}");
                return;
            }
            try
            {
                var url = GlobalConfig.HeishaUrl + $"command?SetQuietMode={mode}";
                var result = await _requestProvider.GetStringAsync(HttpClientConst.HeishaClient, url)
                    ?? throw new Exception($"{_serviceName}:: SetQuietMode returned null");
                QuietMode = int.Parse(result.Split(" ")[4]);
                LogUtils.AddChangeRecord(Changes, Provider.HeishaMon, HarmonyChangeType.SetQuietMode, $"New mode {mode}");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, $"{_serviceName}:: SetQuietMode failed");
            }
        }

        private void HandleHeatPumpInfo(Heatpump hinfo)
        {
            switch (hinfo.Topic)
            {
                case "TOP7":
                    MainTargetTemp = int.Parse(hinfo.Value);
                    break;
                case "TOP6":
                    MainOutletTemp = double.Parse(hinfo.Value);
                    break;
                case "TOP5":
                    MainInletTemp = double.Parse(hinfo.Value);
                    break;
                case "TOP18":
                    QuietMode = int.Parse(hinfo.Value);
                    break;
                default:
                    break;
            }
        }
    }
}
