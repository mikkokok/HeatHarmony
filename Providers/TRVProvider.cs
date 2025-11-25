using HeatHarmony.Config;
using HeatHarmony.Models;
using static HeatHarmony.Config.GlobalConfig;

namespace HeatHarmony.Providers
{
    public sealed class TRVProvider
    {
        private readonly string _serviceName;
        private readonly ILogger<TRVProvider> _logger;
        private readonly IRequestProvider _requestProvider;
        public Task TRVTask { get; private set; }

        private readonly List<ShellyTRV> _devices = [];

        public TRVProvider(ILogger<TRVProvider> logger, IRequestProvider requestProvider)
        {
            _serviceName = nameof(TRVProvider);
            _logger = logger;
            _requestProvider = requestProvider;
            _devices = ShellyTRVConfig ?? throw new Exception("No TRV devices in config!");
            TRVTask = HandleDeviceStatusUpdates();
        }

        public List<ShellyTRV> GetDevices()
        {
            return _devices;
        }

        public async Task HandleDeviceStatusUpdates()
        {
            while (true)
            {
                await UpdateDeviceStatus(false);
                for (int i = 0; i < 2; i++)
                {
                    var updated = _devices.Where(d => d.Status == TRVStatusEnum.Ok).ToList();
                    if (updated is null)
                    {
                        break;
                    }
                    await UpdateDeviceStatus(true);
                    await Task.Delay(TimeSpan.FromSeconds(10));
                }
                await Task.Delay(TimeSpan.FromMinutes(60));
            }
        }

        public async Task SetHeating(int level)
        {
            foreach (var trv in _devices)
            {
                if (trv.LatestLevel == level)
                {
                    _logger.LogInformation($"{_serviceName}:: SetHeating for {trv.Name} already at level {level}, skipping");
                    continue;
                }
                var url = $"http://{trv.IP}/thermostat/0?pos={level}";
                var result = await _requestProvider.GetAsync<TRVThermoResponse>(HttpClientConst.ShellyClient, url)
                    ?? throw new Exception($"{_serviceName}:: SetHeating returned null for {trv.Name}");
                trv.LatestLevel = result.pos;
                trv.UpdatedAt = DateTime.Now;
                trv.Status = TRVStatusEnum.Ok;
                _logger.LogInformation($"{_serviceName}:: SetHeating to {level} for {trv.Name} succeeded");
            }
        }

        public async Task SetAutoTemp(bool enable, int? target)
        {
            foreach (var trv in _devices)
            {
                if (trv.AutoTemperature == enable)
                {
                    _logger.LogInformation($"{_serviceName}:: SetAutoTemp for {trv.Name} already set to {enable}, skipping");
                    continue;
                }

                const int maxRetries = 3;
                const int baseDelaySeconds = 10;
                
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        string? url;
                        if (enable)
                        {
                            url = $"http://{trv.IP}/settings/thermostat/0/?target_t_enabled=1";
                            if (target != null)
                            {
                                url += $"&target_t={target}";
                            }
                        }
                        else
                        {
                            url = $"http://{trv.IP}/settings/thermostat/0/?target_t_enabled=0";
                        }
                        
                        var result = await _requestProvider.GetAsync<TRVTempControlResponse>(HttpClientConst.ShellyClient, url)
                            ?? throw new Exception($"{_serviceName}:: SetAutoTemp returned null for {trv.Name}");
                        
                        trv.AutoTemperature = result.target_t.enabled;
                        _logger.LogInformation($"{_serviceName}:: SetAutoTemp to {enable} for {trv.Name} succeeded on attempt {attempt}");
                        break; 
                    }
                    catch (Exception ex) when (attempt < maxRetries && (ex is HttpRequestException || ex is TaskCanceledException || ex is TimeoutException))
                    {
                        var delayMs = baseDelaySeconds * (int)Math.Pow(2, attempt - 1); // Exponential backoff: 10s, 20s, 40s
                        _logger.LogWarning(ex, $"{_serviceName}:: SetAutoTemp failed for {trv.Name} on attempt {attempt}/{maxRetries}. Retrying in {delayMs}ms...");
                        await Task.Delay(delayMs);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogCritical(ex, $"{_serviceName}:: SetAutoTemp failed for {trv.Name} on attempt {attempt}/{maxRetries}. No more retries.");
                        break; 
                    }
                }
            }
        }

        private async Task UpdateDeviceStatus(bool retryFailed)
        {
            foreach (var device in _devices)
            {
                if (retryFailed && device.Status != TRVStatusEnum.Ok)
                {
                    await SetDeviceStatus(device);
                }
                else if (!retryFailed)
                {
                    await SetDeviceStatus(device);
                }
            }
        }

        private async Task SetDeviceStatus(ShellyTRV device)
        {
            var url = $"http://{device.IP}/status";
            try
            {
                var result = await _requestProvider.GetAsync<TRVStatusResponse>(HttpClientConst.ShellyClient, url)
                    ?? throw new Exception($"{_serviceName}:: InitDevices returned null for {device.Name}");
                device.UpdatedAt = DateTime.Now;
                device.BatteryLevel = result.bat.value;
                device.Status = TRVStatusEnum.Ok;
                device.LatestLevel = result.thermostats.First().pos;
                device.AutoTemperature = result.thermostats.First().target_t.enabled;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, $"{_serviceName}:: UpdateDeviceStatus failed for {device.Name} at {url}");
            }
            device.UpdatedAt = DateTime.Now;
            device.Status = TRVStatusEnum.Error;
        }
    }
}
