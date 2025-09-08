using HeatHarmony.Config;

namespace HeatHarmony.Providers
{
    public class OumanProvider
    {
        private readonly string _serviceName;
        private readonly ILogger<OumanProvider> _logger;
        private readonly IRequestProvider _requestProvider;
        public double LatestOutsideTemp { get; private set; }
        public double LatestFlowTemp { get; private set; }
        public double LatestInsideTemp { get; private set; }
        public double LatestMinFlowTemp { get; private set; }
        public bool AutoDrive { get; private set; } = true;
        public Task OumanTask { get; private set; }

        public OumanProvider(ILogger<OumanProvider> logger, IRequestProvider requestProvider)
        {
            _serviceName = nameof(OumanProvider);
            _logger = logger;
            _requestProvider = requestProvider;
            OumanTask = GetLatestReadings();

        }
        public async Task GetLatestReadings()
        {
            while (true)
            {
                var url = GlobalConfig.OumanConfig!.Url + "request?S_275_85;S_227_85;S_54_85;S_81_85";
                var result = await _requestProvider.GetAsync<string>(HttpClientConst.OumanClient, url);
                if (result != null)
                {
                    var readings = result.Split("?")[1];
                    SetLatest(readings);
                }
                else
                {
                    _logger.LogWarning($"{_serviceName}:: GetLatestReadings returned null");
                }
                await Task.Delay(TimeSpan.FromMinutes(5));
            }
        }
        public async Task Login() {
            var url = GlobalConfig.OumanConfig!.Url + "login?uid=" + GlobalConfig.OumanConfig?.Username + ";pwd=" + GlobalConfig.OumanConfig?.Password;
            await _requestProvider.GetAsync<string>(HttpClientConst.OumanClient, url);
        }

        public async Task SetMinFlowTemp(int newTemp)
        {
            var url = GlobalConfig.OumanConfig!.Url + "update?@_S_54_85=" + newTemp + ";";
            var result = await _requestProvider.GetAsync<string>(HttpClientConst.OumanClient, url);
            if (result != null) {
                var reading = result.Split("?")[1].Split("=")[1];
                LatestMinFlowTemp = double.Parse(reading);
            }
        }

        public async Task SetInsideTemp(int newTemp)
        {
            var url = GlobalConfig.OumanConfig!.Url + "update?@_S_81_85=" + newTemp + ";";
            var result = await _requestProvider.GetAsync<string>(HttpClientConst.OumanClient, url);
            if (result != null)
            {
                var reading = result.Split("?")[1].Split("=")[1];
                LatestInsideTemp = double.Parse(reading);
            }
        }

        public async Task SetMaximumFlow()
        {
            var url = GlobalConfig.OumanConfig!.Url + "update?S_59_85=6;S_92_85=100;";
            var result = await _requestProvider.GetAsync<string>(HttpClientConst.OumanClient, url);
            if (result != null)
            {
                var reading = result.Split("?")[1].Split("=")[1];
                var max = double.Parse(reading);
                if (max == 100)
                {
                    AutoDrive = false;
                }
            }
        }

        public async Task SetAutoDriveOn()
        {
            var url = GlobalConfig.OumanConfig!.Url + "update?S_59_85=0;";
            var result = await _requestProvider.GetAsync<string>(HttpClientConst.OumanClient, url);
            if (result != null)
            {
                AutoDrive = true;
            }
        }

        private void SetLatest(string kvPair)
        {
            var pairs = kvPair.Split(";");
            foreach (var pair in pairs)
            {
                var splitted = pair.Split("=");
                var code = splitted[0];
                var value = splitted[1];
                switch (code)
                {
                    case "S_275_85":
                        LatestOutsideTemp = double.Parse(value);
                        break;
                    case "S_227_85":
                        LatestFlowTemp = double.Parse(value);
                        break;
                    case "S_54_85":
                        LatestMinFlowTemp = double.Parse(value);
                        break;
                    case "S_81_85":
                        LatestInsideTemp = double.Parse(value);
                        break;
                    default:
                        _logger.LogWarning($"{_serviceName}:: SetLatest unknown code {code}");
                        break;
                }
            }
        }
    }
}