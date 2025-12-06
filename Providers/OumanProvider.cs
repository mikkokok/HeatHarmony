using HeatHarmony.Config;
using HeatHarmony.Models;
using HeatHarmony.Utils;

namespace HeatHarmony.Providers
{
    public sealed class OumanProvider
    {
        private readonly string _serviceName;
        private readonly ILogger<OumanProvider> _logger;
        private readonly IRequestProvider _requestProvider;
        public double LatestFlowDemand { get; private set; }
        public double LatestOutsideTemp { get; private set; }
        public double LatestInsideTempDemand { get; private set; }
        public double LatestMinFlowTemp { get; private set; }
        public bool AutoTemp { get; private set; } = true;
        public Task OumanTask { get; private set; }
        public List<HarmonyChange> Changes { get; private set; } = [];
        public double LatestInsideTemp { get; private set; }

        public OumanProvider(ILogger<OumanProvider> logger, IRequestProvider requestProvider)
        {
            _serviceName = nameof(OumanProvider);
            _logger = logger;
            _requestProvider = requestProvider;
            OumanTask = GetLatestReadings();
        }

        private async Task GetLatestReadings()
        {
            while (true)
            {
                try
                {
                    var url = GlobalConfig.OumanConfig!.Url + "request?S_275_85;S_227_85;S_54_85;S_81_85;S_59_85;S_284_85";
                    var result = await _requestProvider.GetStringAsync(HttpClientConst.OumanClient, url);
                    if (result != null)
                    {
                        var readings = result.Split("?")[1];
                        SetLatest(readings);
                    }
                    else
                    {
                        _logger.LogWarning("{service}:: GetLatestReadings returned null", _serviceName);
                    }
                    await Task.Delay(TimeSpan.FromMinutes(5));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{service}:: GetLatestReadings exception", _serviceName);
                    await Task.Delay(TimeSpan.FromMinutes(1));
                }
            }
        }

        public async Task SetMinFlowTemp(int newTemp)
        {
            await Login();
            var url = GlobalConfig.OumanConfig!.Url + "update?@_S_54_85=" + newTemp + ";";
            var result = await _requestProvider.GetStringAsync(HttpClientConst.OumanClient, url);
            if (result != null)
            {
                var reading = result.Split("?")[1].Split("=")[1].Split(";")[0];
                LatestMinFlowTemp = double.Parse(reading);
                LogUtils.AddChangeRecord(Changes, Provider.Ouman, HarmonyChangeType.SetMinFlowTemp, $"New temp {newTemp}");
            }
        }

        public async Task SetInsideTemp(double newTemp)
        {
            await Login();
            var url = GlobalConfig.OumanConfig!.Url + "update?@_S_81_85=" + newTemp + ";";
            var result = await _requestProvider.GetStringAsync(HttpClientConst.OumanClient, url);
            if (result != null)
            {
                var reading = result.Split("?")[1].Split("=")[1].Split(";")[0];
                LatestInsideTempDemand = double.Parse(reading);
                LogUtils.AddChangeRecord(Changes, Provider.Ouman, HarmonyChangeType.SetInsideTemp, $"New temp {newTemp}");
            }
        }

        public async Task SetMaximumFlow()
        {
            await Login();
            var url = GlobalConfig.OumanConfig!.Url + "update?S_59_85=6;S_92_85=100;";
            var result = await _requestProvider.GetStringAsync(HttpClientConst.OumanClient, url);
            if (result != null)
            {
                var reading = result.Split("?")[1].Split("=")[1].Split(";")[0];
                var max = double.Parse(reading);
                if (max == 100)
                {
                    AutoTemp = false;
                }
            }
            LogUtils.AddChangeRecord(Changes, Provider.Ouman, HarmonyChangeType.SetMaximumFlow);
        }

        public async Task SetAutoDriveOn()
        {
            if (AutoTemp)
                return;
            await Login();
            var url = GlobalConfig.OumanConfig!.Url + "update?S_59_85=0;";
            var result = await _requestProvider.GetStringAsync(HttpClientConst.OumanClient, url);
            if (result != null)
            {
                AutoTemp = true;
            }
            LogUtils.AddChangeRecord(Changes, Provider.Ouman, HarmonyChangeType.SetAutoDriveOn);
        }

        public async Task SetDefault()
        {
            await SetAutoDriveOn();
            await SetInsideTemp(20);
            await SetMinFlowTemp(20);
            LogUtils.AddChangeRecord(Changes, Provider.Ouman, HarmonyChangeType.SetDefault);
        }

        public async Task SetConservativeHeating()
        {
            await SetAutoDriveOn();
            await SetMinFlowTemp(20);
        }

        private void SetLatest(string kvPair)
        {
            var pairs = kvPair.Split(";");
            foreach (var pair in pairs)
            {
                if (pair == "\0")
                {
                    continue;
                }
                var splitted = pair.Split("=");
                var code = splitted[0];
                var value = splitted[1];
                switch (code)
                {
                    case "S_275_85":
                        LatestFlowDemand = double.Parse(value);
                        break;
                    case "S_227_85":
                        LatestOutsideTemp = double.Parse(value);
                        break;
                    case "S_54_85":
                        LatestMinFlowTemp = double.Parse(value);
                        break;
                    case "S_81_85":
                        LatestInsideTempDemand = double.Parse(value);
                        break;
                    case "S_59_85":
                        AutoTemp = value == "0";
                        break;
                    case "S_284_85":
                        LatestInsideTemp = double.Parse(value);
                        break;
                    default:
                        _logger.LogWarning("{service}:: SetLatest unknown code {code}", _serviceName, code);
                        break;
                }
            }
        }

        private async Task Login()
        {
            var url = GlobalConfig.OumanConfig!.Url + "login?uid=" + GlobalConfig.OumanConfig!.Username + ";pwd=" + GlobalConfig.OumanConfig!.Password;
            await _requestProvider.GetStringAsync(HttpClientConst.OumanClient, url);
        }
    }
}