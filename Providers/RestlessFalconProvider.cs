using HeatHarmony.Config;
using HeatHarmony.DTO;

namespace HeatHarmony.Providers
{
    public sealed class RestlessFalconProvider(ILogger<RestlessFalconProvider> logger, IRequestProvider requestProvider)
    {
        private readonly string _serviceName = nameof(RestlessFalconProvider);
        private readonly ILogger<RestlessFalconProvider> _logger = logger;
        private readonly IRequestProvider _requestProvider = requestProvider;
        private readonly Dictionary<int, (double value, DateTime cachedAt)> _cache = [];
        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(12);

        public async Task<double?> GetAvgTemperature(int days)
        {
            if (days < 0)
            {
                _logger.LogWarning("{ServiceName}:: GetAvgTemperature received invalid number of days: {Days}", _serviceName, days);
                return null;
            }

            if (_cache.TryGetValue(days, out var cached) && DateTime.Now - cached.cachedAt < CacheDuration)
            {
                _logger.LogDebug("{ServiceName}:: Returning cached avg temperature {Temp:F1}°C for {Days} days (cached {Ago:F1}h ago)",
                    _serviceName, cached.value, days, (DateTime.Now - cached.cachedAt).TotalHours);
                return cached.value;
            }

            var url = $"{GlobalConfig.RestlessFalconConfig?.Url}SensorData?id=5&ago={days}&amount=0";

            try
            {
                var result = await _requestProvider.GetAsync<List<FalconResponse>>(HttpClientConst.RestlessFalconClient, url)
                    ?? throw new Exception($"{_serviceName}:: GetAvgTemperature returned null");
                if (result.Count == 0)
                {
                    _logger.LogWarning("{ServiceName}:: GetAvgTemperature received empty data for {Days} days", _serviceName, days);
                    return _cache.TryGetValue(days, out var stale) ? stale.value : null;
                }
                var avgTemp = result.Average(r => r.temperature);
                _cache[days] = (avgTemp, DateTime.Now);
                _logger.LogInformation("{ServiceName}:: GetAvgTemperature calculated average temperature of {AvgTemp}°C from {Count} records for {Days} days", _serviceName, avgTemp, result.Count, days);
                return avgTemp;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{ServiceName}:: Exception occurred while fetching average temperature from Restless Falcon: {ErrorMessage}", _serviceName, ex.Message);
                return _cache.TryGetValue(days, out var stale) ? stale.value : null;
            }
        }
    }
}
