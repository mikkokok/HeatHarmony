using HeatHarmony.Config;
using HeatHarmony.Models;
using System.Globalization;

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
        private int _priceHour = 15;
        public CultureInfo finnishCulture = new("fi-FI");
        public PriceProvider(ILogger<PriceProvider> logger, IRequestProvider requestProvider)
        {
            _serviceName = nameof(PriceProvider);
            _logger = logger;
            _requestProvider = requestProvider;
            PriceTask = UpdatePrices();
        }
        public async Task UpdatePrices()
        {
            await UpdatePriceLists();
            while (true)
            {
                if (DateTime.Now.Hour == _priceHour)
                {
                    await UpdatePriceLists();

                    DateTime parsedDate = DateTime.ParseExact(TomorrowPrices[0].date, GlobalConst.PriceTimeFormat, finnishCulture);
                    if (parsedDate.Day <= DateTime.Now.Day)
                    {
                        _priceHour++;
                        if (_priceHour > 22)
                        {
                            _priceHour = 15;
                        }
                    }
                    else
                    {
                        _priceHour = 15;
                    }
                    await Task.Delay(TimeSpan.FromMinutes(20));
                }
            }
        }

        private async Task UpdatePriceLists()
        {
            try
            {
                foreach (var urlSuffix in new string[] { "true", "false" })
                {
                    var url = GlobalConfig.PricesUrl + urlSuffix;
                    var result = await _requestProvider.GetAsync<List<ElectricityPrice>>(HttpClientConst.PriceClient, url)
                        ?? throw new Exception($"{_serviceName}:: UpdatePriceLists returned null from {url}");
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
            catch (Exception ex)
            {
                _logger.LogCritical(ex, $"{_serviceName}:: UpdatePriceLists failed");
            }
        }
    }
}
