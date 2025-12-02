using HeatHarmony.Config;
using HeatHarmony.Models;
using HeatHarmony.Utils;

namespace HeatHarmony.Providers
{
    public sealed class EMProvider(ILogger<EMProvider> logger, IRequestProvider requestProvider)
    {
        private readonly string _serviceName = nameof(EMProvider);
        private readonly ILogger<EMProvider> _logger = logger;
        private readonly IRequestProvider _requestProvider = requestProvider;
        public DateTime LastEnabled { get; private set; } = DateTime.Now;
        private DateTime? _overrideUntil;
        private const int _maxOverrideHours = 48;
        public bool IsOn { get; private set; }

        public bool IsOverridden
        {
            get
            {
                if (_overrideUntil is null) return false;
                if (DateTime.Now <= _overrideUntil.Value) return true;
                _overrideUntil = null;
                return false;
            }
            private set
            {
                if (value)
                {
                    _overrideUntil = DateTime.Now.AddHours(_maxOverrideHours);
                }
                else
                {
                    _overrideUntil = null;
                }
            }
        }

        public List<HarmonyChange> Changes { get; private set; } = [];

        public async Task EnableWaterHeating()
        {
            try
            {
                var url = GlobalConfig.Shelly3EMUrl + "relay/0?turn=on";
                var result = await _requestProvider.GetAsync<EMRelayResponse>(HttpClientConst.Shelly3EMClient, url)
                    ?? throw new Exception($"{_serviceName}:: EnableWaterHeating returned null");
                IsOn = result.IsOn;
                if (!result.IsOn)
                {
                    _logger.LogWarning($"{_serviceName}:: EnableWaterHeating did not turn on the relay.");
                }
                else
                {
                    LastEnabled = DateTime.Now;
                }
                LogUtils.AddChangeRecord(Changes, Provider.EM, HarmonyChangeType.EnableWaterHeating, "Water heating enabled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{_serviceName}:: Exception occurred while switching relay on from EM.");
            }
        }

        public async Task DisableWaterHeating()
        {
            try
            {
                var url = GlobalConfig.Shelly3EMUrl + "relay/0?turn=off";
                var result = await _requestProvider.GetAsync<EMRelayResponse>(HttpClientConst.Shelly3EMClient, url)
                    ?? throw new Exception($"{_serviceName}:: DisableWaterHeating returned null");
                IsOn = result.IsOn;
                if (result.IsOn)
                {
                    _logger.LogWarning($"{_serviceName}:: DisableWaterHeating did not turn off the relay.");
                }
                _overrideUntil = null;

                LogUtils.AddChangeRecord(Changes, Provider.EM, HarmonyChangeType.DisableWaterHeating, "Water heating disabled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{_serviceName}:: Exception occurred while switching relay off from EM.");
            }
        }

        public async Task OverrideEnable(int? overrideHours = null)
        {
            await EnableWaterHeating();
            var hours = overrideHours ?? _maxOverrideHours;
            _overrideUntil = DateTime.Now.AddHours(hours);

            LogUtils.AddChangeRecord(Changes, Provider.EM, HarmonyChangeType.OverrideEnable,
                $"Manual override ON. Max duration: {hours}h (until {_overrideUntil:yyyy-MM-dd HH:mm}).");
        }

        public void ClearOverride()
        {
            _overrideUntil = null;
            LogUtils.AddChangeRecord(Changes, Provider.EM, HarmonyChangeType.OverrideEnable, "Override cleared.");
        }

        public bool HasRunEnough()
        {
            var hasRunEnough = (DateTime.Now - LastEnabled).TotalHours >= 3;
            return hasRunEnough;
        }

        public bool IsRunning()
        {
            try
            {
                var url = GlobalConfig.Shelly3EMUrl + "status";
                var result = _requestProvider.GetAsync<EMStatusResponse>(HttpClientConst.Shelly3EMClient, url).Result
                    ?? throw new Exception($"{_serviceName}:: IsRunning returned null");
                IsOn = result.relays[0].IsOn;
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
