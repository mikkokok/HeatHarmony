using HeatHarmony.Config;
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

        public Pro3Provider(ILogger<Pro3Provider> logger, IRequestProvider requestProvider)
        {
            _logger = logger;
            _requestProvider = requestProvider;
            InitDevices();
        }

        public async Task GetDeviceStatus()
        {
            foreach (var deviceId in deviceIds)
            {
                var url = GlobalConfig.ShellyPro3Url + $"Switch.GetStatus?id={deviceId}";
                var result = await _requestProvider.GetAsync<Pro3StatusResponse>(HttpClientConst.ShellyPro3Client, url)
                    ?? throw new Exception($"{_serviceName}:: GetDeviceStatus returned null");
                UpdateDeviceStatus(result);
                _logger.LogInformation("{service}:: GetDeviceStatus for device {deviceId} returned {output}", _serviceName, deviceId, result.output);
            }
            await Task.Delay(TimeSpan.FromMinutes(5));
        }

        public async Task SetOutput(int outputAmount, bool output)
        {
            if (outputAmount < 1 || outputAmount > 3)
            {
                _logger.LogWarning("{service}:: SetOutput received invalid outputAmount {outputAmount}. Must be between 1 and 3.", _serviceName, outputAmount);
            }
            _logger.LogInformation("{service}:: SetOutput called with outputAmount {outputAmount} and output {output}", _serviceName, outputAmount, output);
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
                _logger.LogWarning("{service}:: UpdateDeviceStatus could not find device with id {deviceId}. Filling in...", _serviceName, status.id);
                _devices.Add(status);
            }
        }

        private async Task SetDeviceOutput(int[] ids, bool output)
        {
            _logger.LogInformation("{service}:: SetDeviceOutput called for devices {deviceIds} with output {output}", _serviceName, string.Join(", ", ids), output);
            foreach (var deviceId in ids)
            {
                var url = GlobalConfig.ShellyPro3Url + $"Switch.Set?id={deviceId}&on={output}";
                var result = await _requestProvider.GetAsync<Pro3SetResponse>(HttpClientConst.ShellyPro3Client, url)
                    ?? throw new Exception($"{_serviceName}:: SetDeviceOutput returned null");
                _logger.LogInformation("{service}:: SetDeviceOutput for device {deviceId} returned was on {output}", _serviceName, deviceId, result.was_on);
            }
        }
    }
}
