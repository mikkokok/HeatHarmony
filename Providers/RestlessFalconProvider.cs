using HeatHarmony.Config;
using HeatHarmony.DTO;

namespace HeatHarmony.Providers
{
    public sealed class RestlessFalconProvider(ILogger<RestlessFalconProvider> logger, IRequestProvider requestProvider)
    {
        private readonly string _serviceName = nameof(RestlessFalconProvider);
        private readonly ILogger<RestlessFalconProvider> _logger = logger;
        private readonly IRequestProvider _requestProvider = requestProvider;
        private double? _cachedAvgTemp;
        private DateTime _cachedAt = DateTime.MinValue;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(12);

        public async Task<double?> GetAvgTemperature(int days)
        {
            if (days < 0)
            {
                _logger.LogWarning("{ServiceName}:: GetAvgTemperature received invalid number of days: {Days}", _serviceName, days);
                return null;
            }

            if (_cachedAvgTemp.HasValue && DateTime.Now - _cachedAt < CacheDuration)
            {
                _logger.LogDebug("{ServiceName}:: Returning cached avg temperature {Temp:F1}°C (cached {Ago:F1}h ago)",
                    _serviceName, _cachedAvgTemp.Value, (DateTime.Now - _cachedAt).TotalHours);
                return _cachedAvgTemp;
            }

            var url = $"{GlobalConfig.RestlessFalconConfig?.Url}SensorData?id=5&ago={days}&amount=0";

            try
            {
                var result = await _requestProvider.GetAsync<List<FalconResponse>>(HttpClientConst.RestlessFalconClient, url)
                    ?? throw new Exception($"{_serviceName}:: GetAvgTemperature returned null");
                if (result.Count == 0)
                {
                    _logger.LogWarning("{ServiceName}:: GetAvgTemperature received empty data for {Days} days", _serviceName, days);
                    return _cachedAvgTemp;
                }
                var avgTemp = result.Average(r => r.temperature);
                _cachedAvgTemp = avgTemp;
                _cachedAt = DateTime.Now;
                _logger.LogInformation("{ServiceName}:: GetAvgTemperature calculated average temperature of {AvgTemp}°C from {Count} records for {Days} days", _serviceName, avgTemp, result.Count, days);
                return avgTemp;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{ServiceName}:: Exception occurred while fetching average temperature from Restless Falcon: {ErrorMessage}", _serviceName, ex.Message);
                return _cachedAvgTemp;
            }
        }
    }
}
