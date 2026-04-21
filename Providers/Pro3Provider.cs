using HeatHarmony.Config;
using HeatHarmony.DTO;
using HeatHarmony.Models;

namespace HeatHarmony.Providers
{
    public sealed class Pro3Provider
    {
        private readonly string _serviceName = nameof(Pro3Provider);
        private readonly ILogger<Pro3Provider> _logger;
        private readonly IRequestProvider _requestProvider;
        private readonly int[] deviceIds = { 0, 1, 2 };
        private readonly List<Pro3StatusResponse> _devices = [];
        private readonly object _overrideLock = new();
        private CancellationTokenSource _overrideCts = new();
        private Task? _overrideTask;
        public Task Pro3StatusTask { get; private set; }
        public bool IsOverridden { get; private set; }
        public DateTime? OverrideUntil { get; private set; }
        public int? OverrideOutputAmount { get; private set; }
        public bool? OverrideOutputState { get; private set; }
        public List<HarmonyChange> Changes { get; private set; } = [];
        public Pro3Provider(ILogger<Pro3Provider> logger, IRequestProvider requestProvider)
        {
            _logger = logger;
            _requestProvider = requestProvider;
            InitDevices();
            Pro3StatusTask = UpdatePro3Status();
        }

        public List<Pro3StatusResponse> GetDeviceStatus()
        {
            return _devices;
        }

        private async Task UpdatePro3Status()
        {
            while (true)
            {
                try
                {
                    await UpdateDeviceStatus();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{ServiceName}:: UpdatePro3Status failed", _serviceName);
                }
                await Task.Delay(TimeSpan.FromMinutes(30));
            }
        }

        private async Task UpdateDeviceStatus()
        {
            foreach (var deviceId in deviceIds)
            {
                var url = GlobalConfig.ShellyPro3Url + $"Switch.GetStatus?id={deviceId}";
                var result = await _requestProvider.GetAsync<Pro3StatusResponse>(HttpClientConst.ShellyPro3Client, url)
                    ?? throw new Exception($"{_serviceName}:: GetDeviceStatus returned null");
                UpdateDeviceStatus(result);
                _logger.LogInformation("{ServiceName}:: GetDeviceStatus for device {DeviceId} returned {Output}", _serviceName, deviceId, result.output);
            }
        }

        public async Task SetOutput(int outputAmount, bool output)
        {
            if (IsOverridden)
            {
                _logger.LogInformation("{ServiceName}:: SetOutput ignored, override is active until {Until}", _serviceName, OverrideUntil);
                return;
            }

            await SetOutputInternal(outputAmount, output);
        }

        public void OverrideOutput(int outputAmount, bool output, int durationMinutes)
        {
            lock (_overrideLock)
            {
                if (_overrideTask is not null)
                {
                    CancelOverrideCtsNoThrow();
                    _logger.LogInformation("{ServiceName}:: Previous override cancelled, starting new one", _serviceName);
                }

                _overrideCts = new CancellationTokenSource();
                _overrideTask = RunOverrideAsync(outputAmount, output, durationMinutes, _overrideCts.Token);
            }
        }

        public void CancelOverride()
        {
            lock (_overrideLock)
            {
                if (!IsOverridden && _overrideTask is null)
                {
                    _logger.LogInformation("{ServiceName}:: No override in place to cancel", _serviceName);
                    return;
                }

                CancelOverrideCtsNoThrow();
                _logger.LogInformation("{ServiceName}:: Override cancellation requested", _serviceName);
            }
        }

        public int GetCurrentOutputAmount()
        {
            return _devices.Count(d => d.output);
        }

        private async Task RunOverrideAsync(int outputAmount, bool output, int durationMinutes, CancellationToken ct)
        {
            var operationId = Guid.NewGuid();
            try
            {
                _logger.LogInformation("{ServiceName}:: [{OperationId}] Override started: outputAmount={OutputAmount}, output={Output} for {DurationMinutes} min",
                    _serviceName, operationId, outputAmount, output, durationMinutes);

                await SetOutputInternal(outputAmount, output);

                lock (_overrideLock)
                {
                    IsOverridden = true;
                    OverrideUntil = DateTime.Now.AddMinutes(durationMinutes);
                    OverrideOutputAmount = outputAmount;
                    OverrideOutputState = output;
                }

                await Task.Delay(TimeSpan.FromMinutes(durationMinutes), ct);

                _logger.LogInformation("{ServiceName}:: [{OperationId}] Override duration elapsed, turning output off", _serviceName, operationId);
                await SetOutputInternal(outputAmount, false);
            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation("{ServiceName}:: [{OperationId}] Override cancelled, turning output off", _serviceName, operationId);
                try
                {
                    await SetOutputInternal(outputAmount, false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "{ServiceName}:: [{OperationId}] Failed to turn off output after override cancellation", _serviceName, operationId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{ServiceName}:: [{OperationId}] Override task failed", _serviceName, operationId);
            }
            finally
            {
                lock (_overrideLock)
                {
                    IsOverridden = false;
                    OverrideUntil = null;
                    OverrideOutputAmount = null;
                    OverrideOutputState = null;
                }
            }
        }

        private async Task SetOutputInternal(int outputAmount, bool output)
        {
            if (outputAmount < 1 || outputAmount > 3)
            {
                _logger.LogWarning("{ServiceName}:: SetOutput received invalid outputAmount {OutputAmount}. Must be between 1 and 3.", _serviceName, outputAmount);
                return;
            }
            _logger.LogInformation("{ServiceName}:: SetOutput called with outputAmount {OutputAmount} and output {Output}", _serviceName, outputAmount, output);
            switch (outputAmount)
            {
                case 1:
                    await SetDeviceOutput([0], output);
                    break;
                case 2:
                    await SetDeviceOutput([0, 1], output);
                    break;
                case 3:
                    await SetDeviceOutput([0, 1, 2], output);
                    break;
            }
        }

        private void CancelOverrideCtsNoThrow()
        {
            try
            {
                _overrideCts.Cancel();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "{ServiceName}:: Exception while cancelling override token", _serviceName);
            }
        }

        private void InitDevices()
        {
            foreach (var deviceId in deviceIds)
            {
                _devices.Add(new Pro3StatusResponse
                {
                    id = deviceId,
                    source = null,
                    output = false,
                    temperature = null
                });
            }
        }

        private void UpdateDeviceStatus(Pro3StatusResponse status)
        {
            var device = _devices.FirstOrDefault(d => d.id == status.id);
            if (device != null)
            {
                device.source = status.source;
                device.output = status.output;
                device.temperature = status.temperature;
            }
            else
            {
                _logger.LogWarning("{ServiceName}:: UpdateDeviceStatus could not find device with id {DeviceId}. Filling in...", _serviceName, status.id);
                _devices.Add(status);
            }
        }

        private async Task SetDeviceOutput(int[] ids, bool output)
        {
            _logger.LogInformation("{ServiceName}:: SetDeviceOutput called for devices {DeviceIds} with output {Output}", _serviceName, string.Join(", ", ids), output);
            foreach (var deviceId in ids)
            {
                var url = GlobalConfig.ShellyPro3Url + $"Switch.Set?id={deviceId}&on={output.ToString().ToLowerInvariant()}";
                var result = await _requestProvider.GetAsync<Pro3SetResponse>(HttpClientConst.ShellyPro3Client, url)
                    ?? throw new Exception($"{_serviceName}:: SetDeviceOutput returned null");
                _logger.LogInformation("{ServiceName}:: SetDeviceOutput for device {DeviceId} returned was on {WasOn}", _serviceName, deviceId, result.was_on);
                Changes.Add(new HarmonyChange
                {
                    Time = DateTime.Now,
                    Provider = Provider.Pro3,
                    ChangeType = HarmonyChangeType.Pro3OutputChange,
                    Description = $"SetDeviceOutput set to {output} for device {deviceId} returned was on {result.was_on}"
                });
            }
        }
    }
}
