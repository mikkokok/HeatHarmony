using HeatHarmony.Config;
using HeatHarmony.Models;
using System.Globalization;
using HeatHarmony.Utils;

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

        private record ParsedPrice(DateTime DateTime, decimal Price, ElectricityPrice Original);
        private record HourlyGroup(DateTime HourStart, List<ParsedPrice> Slots, decimal AveragePrice);

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

                    if (TomorrowPrices.Count > 0)
                    {
                        try
                        {
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
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, $"{_serviceName}:: Error parsing tomorrow's first date");
                            _priceHour = 15;
                        }
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
                        TodayLowPriceTimes = CalculateLowPriceTimesWithRanks(TodayPrices);
                    }
                    else
                    {
                        TomorrowPrices = result;
                        TomorrowLowPriceTimes = CalculateLowPriceTimesWithRanks(TomorrowPrices);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, $"{_serviceName}:: UpdatePriceLists failed");
            }
        }

        private List<LowPriceDateTimeRange> CalculateLowPriceTimesWithRanks(List<ElectricityPrice> prices)
        {
            if (prices.Count == 0)
                return [];

            var sortedPrices = new List<ParsedPrice>();
            
            foreach (var price in prices)
            {
                try
                {
                    var dateTime = DateTime.ParseExact(price.date, GlobalConst.PriceTimeFormat, CultureInfo.InvariantCulture);
                    var priceValue = decimal.Parse(price.price, NumberStyles.Float, CultureInfo.InvariantCulture);
                    sortedPrices.Add(new ParsedPrice(dateTime, priceValue, price));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"{_serviceName}:: Error parsing price data: date='{price.date}', price='{price.price}'");
                }
            }

            if (sortedPrices.Count == 0)
                return [];

            sortedPrices = [.. sortedPrices.OrderBy(p => p.DateTime)];

            var allPeriods = CreateHourBasedPeriods(sortedPrices);
            
            var sortedByPrice = allPeriods
                .OrderBy(p => p.AveragePrice)
                .ThenBy(p => p.Start)
                .ToList();
            
            var rankedPeriods = new List<LowPriceDateTimeRange>();
            int rank = 1;
            
            foreach (var period in sortedByPrice.Where(p => TimeUtils.GetHoursInRange(p) >= 1.0))
            {
                if (rank <= 5)
                {
                    period.Rank = rank++;
                    rankedPeriods.Add(period);
                }
            }
            
            foreach (var period in sortedByPrice.Where(p => !rankedPeriods.Contains(p)))
            {
                period.Rank = rank++;
                rankedPeriods.Add(period);
            }
            
            rankedPeriods.Sort((a, b) => a.Rank.CompareTo(b.Rank));
            
            _logger.LogInformation($"{_serviceName}:: Found {rankedPeriods.Count} ranked periods. " +
                $"Top 5 ranks are full-hour+ periods. Total coverage: {rankedPeriods.Sum(r => TimeUtils.GetHoursInRange(r)):F1} hours");

            return rankedPeriods;
        }

        private static List<LowPriceDateTimeRange> CreateHourBasedPeriods(List<ParsedPrice> sortedPrices)
        {
            var periods = new List<LowPriceDateTimeRange>();
            
            var hourlyGroups = sortedPrices
                .GroupBy(p => new DateTime(p.DateTime.Year, p.DateTime.Month, p.DateTime.Day, p.DateTime.Hour, 0, 0))
                .OrderBy(g => g.Key)
                .Select(g => new HourlyGroup(
                    g.Key,
                    [.. g.OrderBy(s => s.DateTime)],
                    g.Average(s => s.Price)
                ))
                .ToList();
            
            if (hourlyGroups.Count == 0)
                return periods;
            
            int currentIndex = 0;
            
            while (currentIndex < hourlyGroups.Count)
            {
                var periodGroups = new List<HourlyGroup> { hourlyGroups[currentIndex] };
                int nextIndex = currentIndex + 1;
                
                while (nextIndex < hourlyGroups.Count)
                {
                    var currentAvg = periodGroups.Average(g => g.AveragePrice);
                    var nextGroup = hourlyGroups[nextIndex];
                    
                    var lastGroup = periodGroups[^1];
                    bool isConsecutiveHour = nextGroup.HourStart == lastGroup.HourStart.AddHours(1);
                    bool isSimilarPrice = Math.Abs(nextGroup.AveragePrice - currentAvg) <= Math.Max(currentAvg * 0.2m, 0.01m);
                    
                    if (isConsecutiveHour && isSimilarPrice)
                    {
                        periodGroups.Add(nextGroup);
                        nextIndex++;
                    }
                    else
                    {
                        break;
                    }
                }
                
                var allSlots = periodGroups.SelectMany(g => g.Slots).OrderBy(s => s.DateTime).ToList();
                
                if (allSlots.Count > 0)
                {
                    var start = allSlots[0].DateTime;
                    var end = allSlots[^1].DateTime.AddMinutes(15);
                    var averagePrice = allSlots.Average(s => s.Price);
                    
                    periods.Add(new LowPriceDateTimeRange
                    {
                        Start = start,
                        End = end,
                        AveragePrice = averagePrice,
                        Rank = 0
                    });
                }
                
                currentIndex = nextIndex;
            }
            
            return periods;
        }
    }
}
