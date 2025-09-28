using HeatHarmony.Config;
using HeatHarmony.Models;
using System.Globalization;

namespace HeatHarmony.Providers
{
    public sealed class PriceProvider
    {
        private readonly string _serviceName;
        private readonly ILogger<PriceProvider> _logger;
        private readonly IRequestProvider _requestProvider;
        public List<ElectricityPrice> TodayPrices { get; private set; } = [];
        public List<ElectricityPrice> TomorrowPrices { get; private set; } = [];
        public List<LowPriceDateTimeRange> TodayLowPriceTimes { get; private set; } = [];
        public List<LowPriceDateTimeRange> TomorrowLowPriceTimes { get; private set; } = [];
        public Task PriceTask { get; private set; }
        private int _priceHour = 15;
        private decimal _startingPriceThreshold = 0.01m; // 1 cent per kWh
        public PriceProvider(ILogger<PriceProvider> logger, IRequestProvider requestProvider)
        {
            _serviceName = nameof(PriceProvider);
            _logger = logger;
            _requestProvider = requestProvider;
            PriceTask = UpdatePrices();
        }
        private async Task UpdatePrices()
        {
            await UpdatePriceLists();
            while (true)
            {
                if (DateTime.Now.Hour == _priceHour)
                {
                    await UpdatePriceLists();

                    DateTime parsedDate = DateTime.ParseExact(TomorrowPrices[0].date, GlobalConst.PriceTimeFormat, CultureInfo.InvariantCulture);
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
                    await Task.Delay(TimeSpan.FromMinutes(30));
                }
                await Task.Delay(TimeSpan.FromSeconds(10));
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
                //var TodayLowPriceBlocks = FindConsecutiveLowPriceBlocks(TodayPrices, LowPriceThreshold);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, $"{_serviceName}:: UpdatePriceLists failed");
            }
        }

        //private List<List<ElectricityPrice>> FindConsecutiveLowPriceBlocks(IEnumerable<ElectricityPrice> prices, decimal threshold)
        //{
        //    var ordered = prices
        //        .Select(p =>
        //        {
        //            DateTime? ts = null;
        //            try
        //            {
        //                ts = DateTime.ParseExact(p.date, GlobalConst.PriceTimeFormat, CultureInfo.InvariantCulture);
        //            }
        //            catch (Exception e)
        //            {
        //                _logger.LogWarning(e, "{Service}:: Unable to parse date '{Date}'", _serviceName, p.date);
        //            }
        //            return new { Price = p, Time = ts };
        //        })
        //        .Where(x => x.Time.HasValue)
        //        .OrderBy(x => x.Time)
        //        .ToList();

        //    var result = new List<List<ElectricityPrice>>();
        //    var current = new List<ElectricityPrice>();
        //    DateTime? prev = null;

        //    foreach (var entry in ordered)
        //    {
        //        if (!decimal.TryParse(entry.Price.price, NumberStyles.Any, CultureInfo.InvariantCulture, out var priceValue))
        //        {
        //            _logger.LogWarning("{Service}:: Could not parse price '{Value}'", _serviceName, entry.Price.price);
        //            CloseCurrentIfEligible();
        //            prev = entry.Time;
        //            continue;
        //        }

        //        bool isLow = priceValue < threshold;

        //        if (isLow)
        //        {
        //            if (current.Count == 0)
        //            {
        //                current.Add(entry.Price);
        //            }
        //            else
        //            {
        //                if (prev.HasValue && entry.Time.Value - prev.Value == SlotSpan)
        //                {
        //                    current.Add(entry.Price);
        //                }
        //                else
        //                {
        //                    // Gap -> close previous block
        //                    CloseCurrentIfEligible();
        //                    current.Clear();
        //                    current.Add(entry.Price);
        //                }
        //            }
        //        }
        //        else
        //        {
        //            // Price too high -> close block if long enough
        //            CloseCurrentIfEligible();
        //            current.Clear();
        //        }

        //        prev = entry.Time;
        //    }

        //    // Close tail
        //    CloseCurrentIfEligible();

        //    return result;

        //    void CloseCurrentIfEligible()
        //    {
        //        if (current.Count >= 4) // 4 * 15min = at least 1 hour
        //        {
        //            result.Add([.. current]);
        //        }
        //    }
        //}
    }
}
