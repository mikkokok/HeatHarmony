using HeatHarmony.Config;
using HeatHarmony.Models;

namespace HeatHarmony.Providers
{
    public sealed class EMProvider(ILogger<EMProvider> logger, IRequestProvider requestProvider)
    {
        private readonly string _serviceName = nameof(EMProvider);
        private readonly ILogger<EMProvider> _logger = logger;
        private readonly IRequestProvider _requestProvider = requestProvider;
        public DateTime? LastEnabled { get; private set; } = null;
        public bool IsOverridden { get; private set; } = false;

        public async Task EnableWaterHeating()
        {
            try
            {
                var url = GlobalConfig.Shelly3EMUrl + "relay/0?turn=on";
                var result = await _requestProvider.GetAsync<EMRelayResponse>(HttpClientConst.Shelly3EMClient, url)
                    ?? throw new Exception($"{_serviceName}:: EnableWaterHeating returned null");
                if (!result.IsOn)
                {
                    _logger.LogWarning($"{_serviceName}:: EnableWaterHeating did not turn on the relay.");
                }
                else
                {
                    LastEnabled = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{_serviceName}:: Exception occurred while switching relay on from EM.");
            }
        }

        public async Task DisableWaterHeating(bool Override = false)
        {
            try
            {
                if (!Override)
                {
                    return;
                }
                var url = GlobalConfig.Shelly3EMUrl + "relay/0?turn=off";
                var result = await _requestProvider.GetAsync<EMRelayResponse>(HttpClientConst.Shelly3EMClient, url)
                    ?? throw new Exception($"{_serviceName}:: DisableWaterHeating returned null");
                if (result.IsOn)
                {
                    _logger.LogWarning($"{_serviceName}:: DisableWaterHeating did not turn off the relay.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{_serviceName}:: Exception occurred while switching relay off from EM.");
            }
        }

        public async Task OverrideEnable()
        {
            await EnableWaterHeating();
            IsOverridden = true;
        }
        public bool HasRunEnough()
        {
            if (LastEnabled == null)
            {
                return false;
            }
            var hasRunEnough = (DateTime.Now - LastEnabled.Value).TotalHours >= 3;
            if (hasRunEnough)
            {
                IsOverridden = false;
            }
            return hasRunEnough;
            
        }

        public bool IsRunning()
        {
            try
            {
                var url = GlobalConfig.Shelly3EMUrl + "/status";
                var result = _requestProvider.GetAsync<EMStatusResponse>(HttpClientConst.Shelly3EMClient, url).Result
                    ?? throw new Exception($"{_serviceName}:: IsRunning returned null");
                if (result.total_power > 100)
                    return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{_serviceName}:: Exception occurred while checking if water heating is running.");
            }
            return false;
        }
    }
}
