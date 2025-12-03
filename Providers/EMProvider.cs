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
        public bool IsOn { get; private set; }
        public DateTime? OverrideUntil;
        private const int _maxOverrideHours = 48;
        public EMOverrideMode OverrideMode = EMOverrideMode.None;
        private readonly CancellationTokenSource _overrideCTokenSource = new();
        public bool IsOverrideActive => OverrideUntil is not null && DateTime.Now <= OverrideUntil.Value && OverrideMode != EMOverrideMode.None;
        public List<HarmonyChange> Changes { get; private set; } = [];
        public bool IsOverridden
        {
            get
            {
                if (!IsOverrideActive)
                {
                    OverrideMode = EMOverrideMode.None;
                    OverrideUntil = null;
                    return false;
                }
                return true;
            }
        }

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
                OverrideUntil = null;

                LogUtils.AddChangeRecord(Changes, Provider.EM, HarmonyChangeType.DisableWaterHeating, "Water heating disabled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{_serviceName}:: Exception occurred while switching relay off from EM.");
            }
        }

        public bool HasRunEnough()
        {
            return (DateTime.Now - LastEnabled).TotalHours >= 3;
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

        private void ClearOverride()
        {
            var prev = OverrideMode;
            OverrideMode = EMOverrideMode.None;
            OverrideUntil = null;
            _overrideCTokenSource.Cancel();
            LogUtils.AddChangeRecord(Changes, Provider.EM, HarmonyChangeType.OverrideEnable, $"Override cleared (was {prev}).");
        }

        public async Task ApplyOverride(EMOverrideMode mode, int? hours)
        {
            OverrideMode = mode;
            var duration = hours ?? _maxOverrideHours;
            switch (mode) { 
                case EMOverrideMode.None:
                    ClearOverride();
                    break;
                case EMOverrideMode.Disable:
                    await DisableWaterHeating();
                    break;
                case EMOverrideMode.Enable:
                    await EnableWaterHeating();
                    break;
            }
            OverrideUntil = DateTime.Now.AddHours(duration);
            LogUtils.AddChangeRecord(Changes, Provider.EM, HarmonyChangeType.OverrideEnable, $"Override applied: {mode} for {duration} hours.");
            try
            {
                await Task.Delay(TimeSpan.FromHours(duration), _overrideCTokenSource.Token);
            }
            catch (OperationCanceledException) when (_overrideCTokenSource.Token.IsCancellationRequested)
            {
                _logger.LogInformation($"{_serviceName}:: Override cancelled before duration ended.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{_serviceName}:: Exception occurred during override wait.");
            }
            finally
            {
                ClearOverride();
            }
        }
    }
}
