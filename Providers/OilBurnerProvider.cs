using HeatHarmony.Config;
using HeatHarmony.Models;
using HeatHarmony.Utils;
using Microsoft.Extensions.Logging;

namespace HeatHarmony.Providers
{
    public sealed class OilBurnerProvider
    {
        private readonly string _serviceName;
        private readonly ILogger<OilBurnerProvider> _logger;
        private readonly IRequestProvider _requestProvider;
        public Task OilBurnerTask { get; private set; }
        public List<HarmonyChange> Changes { get; private set; } = [];
        public bool IsEnabled { get; private set; }
        public OilBurnerProvider(ILogger<OilBurnerProvider> logger, IRequestProvider requestProvider)
        {
            _serviceName = nameof(OilBurnerProvider);
            _logger = logger;
            _requestProvider = requestProvider;
            OilBurnerTask = GetOilBurnerStatus();
        }

        private async Task GetOilBurnerStatus()
        {
            while (true)
            {
                try
                {
                    var url = GlobalConfig.OilBurnerShellyUrl + "relay/0";
                    var result = await _requestProvider.GetAsync<PMProStatusResponse>(HttpClientConst.OilBurnerShellyClient, url);
                    if (result != null)
                    {
                        IsEnabled = result.ison;
                    }
                    else
                    {
                        _logger.LogWarning("{service}:: GetOilBurnerStatus returned null", _serviceName);
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

        public async Task SetOilBurnerStatus(bool enable)
        {
            try
            {
                var turn = enable ? "on" : "off";
                var url = GlobalConfig.OilBurnerShellyUrl + $"relay/0?turn={turn}";
                var result = await _requestProvider.GetAsync<EMRelayResponse>(HttpClientConst.OilBurnerShellyClient, url)
                    ?? throw new Exception($"{_serviceName}:: SetOilBurnerStatus returned null");
                IsEnabled = result.IsOn;
                if (result.IsOn != enable)
                {
                    _logger.LogWarning("{service}:: SetOilBurnerStatus did not set the oil burner to the desired state", _serviceName);
                }
                else
                {
                    var action = enable ? "enabled" : "disabled";
                    var changeType = enable ? HarmonyChangeType.OilBurnerEnable : HarmonyChangeType.OilBurnerDisable;
                    LogUtils.AddChangeRecord(Changes, Provider.OilBurner, changeType, $"Oil burner {action}.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{service}:: Exception occurred while setting oil burner status", _serviceName);
            }
        }
    }
}
