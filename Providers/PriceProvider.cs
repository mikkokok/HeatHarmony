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
        public List<LowPriceDateTimeRange> AllLowPriceTimes { get; private set; } = [];
        public LowPriceDateTimeRange NightPeriodTimes { get; private set; } = new();

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
                foreach (var urlSuffix in new string[] { "today", "tomorrow" })
                {
                    var url = GlobalConfig.PricesUrl + urlSuffix;
                    var result = await _requestProvider.GetAsync<List<ElectricityPrice>>(HttpClientConst.PriceClient, url)
                        ?? throw new Exception($"{_serviceName}:: UpdatePriceLists returned null from {url}");
                    if (urlSuffix == "today")
                    {
                        TodayPrices = result;
                    }
                    else
                    {
                        TomorrowPrices = result;
                    }
                }
                TodayLowPriceTimes = CalculateLowPriceTimesWithRanks(TodayPrices);
                TomorrowLowPriceTimes = CalculateLowPriceTimesWithRanks(TomorrowPrices);
                AllLowPriceTimes = [.. TodayLowPriceTimes, .. TomorrowLowPriceTimes];
                NightPeriodTimes = GetBestNightPeriod();
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

        private LowPriceDateTimeRange GetBestNightPeriod()
        {
            // Night window should always be between today 22:00 and tomorrow 08:00
            var nightWindowStart = DateTime.Today.AddHours(22);          // 22:00 today
            var nightWindowEnd   = DateTime.Today.AddDays(1).AddHours(8); // 08:00 tomorrow

            var defaultPeriod = new LowPriceDateTimeRange
            {
                Start = nightWindowStart,
                End = nightWindowStart.AddHours(10) > nightWindowEnd ? nightWindowEnd : nightWindowStart.AddHours(10),
                AveragePrice = 0,
                Rank = 0
            };

            if (TodayPrices.Count == 0 || TomorrowPrices.Count == 0)
            {
                _logger.LogWarning($"{_serviceName}:: Insufficient price data for night period calculation - need both today and tomorrow");
                return defaultPeriod;
            }

            var combinedPrices = new List<ParsedPrice>();

            // Collect today 22:00–23:45 (slots with hour >= 22)
            foreach (var price in TodayPrices)
            {
                if (DateTime.TryParseExact(price.date, GlobalConst.PriceTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
                    && dt >= nightWindowStart && dt < DateTime.Today.AddDays(1))
                {
                    TryAddPrice(price, combinedPrices);
                }
            }

            // Collect tomorrow 00:00–07:45 (slots with hour < 8)
            foreach (var price in TomorrowPrices)
            {
                if (DateTime.TryParseExact(price.date, GlobalConst.PriceTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
                    && dt >= DateTime.Today.AddDays(1) && dt < nightWindowEnd)
                {
                    TryAddPrice(price, combinedPrices);
                }
            }

            // Expect up to 10 hours * 4 = 40 slots; minimum search base 6h => 24 slots
            if (combinedPrices.Count < 24)
            {
                _logger.LogWarning($"{_serviceName}:: Insufficient night price data (got {combinedPrices.Count} slots, need ≥24). Returning default 10h window.");
                return defaultPeriod;
            }

            combinedPrices = [.. combinedPrices.OrderBy(p => p.DateTime)];

            _logger.LogInformation($"{_serviceName}:: Night window slot span {combinedPrices.First().DateTime:yyyy-MM-dd HH:mm} -> {combinedPrices.Last().DateTime:yyyy-MM-dd HH:mm} ({combinedPrices.Count} slots)");

            return FindOptimalNightPeriod(combinedPrices) ?? defaultPeriod;
        }

        private LowPriceDateTimeRange? FindOptimalNightPeriod(List<ParsedPrice> nightPrices)
        {
            // nightPrices already constrained to 22:00–08:00 and ordered
            var candidates = new List<LowPriceDateTimeRange>();

            // Evaluate target lengths 6..10 hours (inclusive)
            for (int targetHours = 6; targetHours <= 10; targetHours++)
            {
                int slotsNeeded = targetHours * 4; // 15 min resolution
                if (nightPrices.Count < slotsNeeded)
                    break;

                for (int startIndex = 0; startIndex <= nightPrices.Count - slotsNeeded; startIndex++)
                {
                    var window = nightPrices.Skip(startIndex).Take(slotsNeeded).ToList();
                    if (!IsContinuousPeriod(window))
                        continue;

                    var start = window[0].DateTime;
                    var end = window[^1].DateTime.AddMinutes(15);
                    var avg = window.Average(s => s.Price);

                    candidates.Add(new LowPriceDateTimeRange
                    {
                        Start = start,
                        End = end,
                        AveragePrice = avg,
                        Rank = 0
                    });
                }
            }

            if (candidates.Count == 0)
            {
                _logger.LogWarning($"{_serviceName}:: No continuous night period candidates found");
                return null;
            }

            // Choose lowest average price; if tie prefer longer; then earliest start
            var best = candidates
                .OrderBy(c => c.AveragePrice)
                .ThenByDescending(c => TimeUtils.GetHoursInRange(c))
                .ThenBy(c => c.Start)
                .First();

            best.Rank = 1;

            _logger.LogInformation($"{_serviceName}:: Best night period: {best.Start:HH:mm}-{best.End:HH:mm} " +
                                   $"({TimeUtils.GetHoursInRange(best):F1}h) avg {best.AveragePrice:F4}");

            return best;
        }

        private static bool IsContinuousPeriod(List<ParsedPrice> slots)
        {
            if (slots == null || slots.Count == 0)
                return false;

            // Ensure chronological order (caller usually provides ordered list, but we guard anyway)
            var ordered = slots.OrderBy(s => s.DateTime).ToList();

            // Each slot must be exactly 15 minutes after the previous (strict continuity, including midnight rollover)
            for (int i = 1; i < ordered.Count; i++)
            {
                var diff = ordered[i].DateTime - ordered[i - 1].DateTime;
                if (diff != TimeSpan.FromMinutes(15))
                {
                    return false;
                }
            }
            return true;
        }

        private void TryAddPrice(ElectricityPrice price, List<ParsedPrice> list)
        {
            try
            {
                var dateTime = DateTime.ParseExact(price.date, GlobalConst.PriceTimeFormat, CultureInfo.InvariantCulture);
                var priceValue = decimal.Parse(price.price, NumberStyles.Float, CultureInfo.InvariantCulture);
                list.Add(new ParsedPrice(dateTime, priceValue, price));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"{_serviceName}:: Error parsing night price slot: date='{price.date}', price='{price.price}'");
            }
        }
    }
}
