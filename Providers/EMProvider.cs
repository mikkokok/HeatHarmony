using HeatHarmony.Config;
using HeatHarmony.Models;
using HeatHarmony.Utils;
using System.Threading.Tasks;

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
        private CancellationTokenSource _overrideCTokenSource = new();
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
                    _logger.LogWarning("{service}:: EnableWaterHeating did not turn on the relay", _serviceName);
                }
                else
                {
                    LastEnabled = DateTime.Now;
                }
                LogUtils.AddChangeRecord(Changes, Provider.EM, HarmonyChangeType.EnableWaterHeating, "Water heating enabled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{service}:: Exception occurred while switching relay on from EM", _serviceName);
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
                    _logger.LogWarning("{service}:: DisableWaterHeating did not turn off the relay", _serviceName);
                }
                OverrideUntil = null;

                LogUtils.AddChangeRecord(Changes, Provider.EM, HarmonyChangeType.DisableWaterHeating, "Water heating disabled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{service}:: Exception occurred while switching relay off from EM", _serviceName);
            }
        }

        public bool HasRunEnough()
        {
            return (DateTime.Now - LastEnabled).TotalHours >= 3;
        }

        public async Task<bool> IsRunning()
        {
            try
            {
                var url = GlobalConfig.Shelly3EMUrl + "status";
                var result = await _requestProvider.GetAsync<EMStatusResponse>(HttpClientConst.Shelly3EMClient, url)
                    ?? throw new Exception($"{_serviceName}:: IsRunning returned null");
                IsOn = result.relays[0].IsOn;
                if (result.total_power > 100)
                    return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{service}:: Exception occurred while checking if water heating is running", _serviceName);
            }
            return false;
        }

        private readonly object _sync = new();

        private void ClearOverride()
        {
            lock (_sync)
            {
                var prev = OverrideMode;
                OverrideMode = EMOverrideMode.None;
                OverrideUntil = null;
                try { _overrideCTokenSource.Cancel(); } catch { }
                _overrideCTokenSource.Dispose();
                _overrideCTokenSource = new CancellationTokenSource();
                _logger.LogInformation("{service}:: Override cleared (was {prev})", _serviceName, prev);
                LogUtils.AddChangeRecord(Changes, Provider.EM, HarmonyChangeType.OverrideEnable, $"Override cleared (was {prev}).");
            }
        }

        public async Task ApplyOverride(EMOverrideMode mode, int? hours)
        {
            var duration = Math.Clamp(hours ?? _maxOverrideHours, 1, _maxOverrideHours);
            lock (_sync)
            {
                OverrideMode = mode;
                OverrideUntil = DateTime.Now.AddHours(duration);
                try { _overrideCTokenSource.Cancel(); } catch { }
                _overrideCTokenSource.Dispose();
                _overrideCTokenSource = new CancellationTokenSource();
            }

            switch (mode)
            {
                case EMOverrideMode.None:
                    ClearOverride();
                    return;
                case EMOverrideMode.Disable:
                    await DisableWaterHeating();
                    break;
                case EMOverrideMode.Enable:
                    await EnableWaterHeating();
                    break;
            }

            _logger.LogInformation("{service}:: Override applied: {mode} for {hours}h until {until}", _serviceName, mode, duration, OverrideUntil);
            LogUtils.AddChangeRecord(Changes, Provider.EM, HarmonyChangeType.OverrideEnable, $"Override applied: {mode} for {duration} hours (until {OverrideUntil}).");

            try
            {
                await Task.Delay(TimeSpan.FromHours(duration), _overrideCTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("{service}:: Override cancelled early", _serviceName);
            }
            finally
            {
                ClearOverride();
            }
        }
    }
}
