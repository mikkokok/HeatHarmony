using HeatHarmony.Config;
using HeatHarmony.Models;
using System;

namespace HeatHarmony.Providers
{
    public class PriceProvider
    {
        private readonly string _serviceName;
        private readonly ILogger<PriceProvider> _logger;
        private readonly IRequestProvider _requestProvider;
        public List<ElectricityPrice> TodayPrices { get; private set; } = [];
        public List<ElectricityPrice> TomorrowPrices { get; private set; } = [];
        public Task PriceTask { get; private set; }
        private readonly int _priceHour = 15;
        public PriceProvider(ILogger<PriceProvider> logger, IRequestProvider requestProvider)
        {
            _serviceName = nameof(PriceProvider);
            _logger = logger;
            _requestProvider = requestProvider;
            PriceTask = UpdatePrices();
        }
        public async Task UpdatePrices()
        {
            while (true)
            {
                try
                {
                    if (DateTime.Now.Hour == _priceHour)
                    {
                        foreach (var urlSuffix in new string[] { "true", "false" })
                        {
                            var url = GlobalConfig.PricesUrl + urlSuffix;
                            var result = await _requestProvider.GetAsync<List<ElectricityPrice>>(HttpClientConst.PriceClient, url)
                                ?? throw new Exception($"{_serviceName}:: UpdatePrices returned null from {url}");
                            if (urlSuffix == "true")
                            {
                                TodayPrices = result;
                            }
                            else
                            {
                                TomorrowPrices = result;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, $"{_serviceName}:: UpdatePrices failed");
                }
                await Task.Delay(TimeSpan.FromMinutes(20));
            }
        }
    }
}
