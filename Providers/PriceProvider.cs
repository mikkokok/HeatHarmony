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
        private readonly RestlessFalconProvider _restlessFalcon;

        public List<ElectricityPrice> TodayPrices { get; private set; } = [];
        public List<ElectricityPrice> TomorrowPrices { get; private set; } = [];
        public List<LowPriceDateTimeRange> TodayLowPriceTimes { get; private set; } = []; 
        public List<LowPriceDateTimeRange> TomorrowLowPriceTimes { get; private set; } = [];
        public List<LowPriceDateTimeRange> AllLowPriceTimes { get; private set; } = [];
        public LowPriceDateTimeRange NightPeriodTimes { get; private set; } = new();
        public LowPriceDateTimeRange DayPeriodTimes { get; private set; } = new();

        public Task PriceTask { get; private set; }
        private int _priceHour = 15;

        private record ParsedPrice(DateTime DateTime, decimal Price, ElectricityPrice Original);
        private record HourlyGroup(DateTime HourStart, List<ParsedPrice> Slots, decimal AveragePrice);
        private double? _last2WeeksAvgTemp = null;

        public PriceProvider(ILogger<PriceProvider> logger, IRequestProvider requestProvider, RestlessFalconProvider restlessFalconProvider)
        {
            _serviceName = nameof(PriceProvider);
            _logger = logger;
            _requestProvider = requestProvider;
            _restlessFalcon = restlessFalconProvider;
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
                _last2WeeksAvgTemp = await _restlessFalcon.GetAvgTemperature(14);
                NightPeriodTimes = GetBestNightPeriod();
                DayPeriodTimes = GetBestDayPeriod();
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

        private (int min, int max) GetNightTargetHours()
        {
            var hours = _last2WeeksAvgTemp switch
            {
                null => (min: 7, max: 10),
                < -10 => (min: 8, max: 10),
                < 0 => (min: 7, max: 10),
                < 5 => (min: 6, max: 9),
                < 10 => (min: 5, max: 8),
                < 15 => (min: 4, max: 6),
                < 20 => (min: 3, max: 5),
                _ => (min: 2, max: 4)
            };
            _logger.LogInformation("{service}:: Night target hours: {min}-{max}h (2-week avg: {temp}°C)",
                _serviceName, hours.min, hours.max, _last2WeeksAvgTemp?.ToString("F1") ?? "N/A");
            return hours;
        }

        private (int min, int max) GetDayTargetHours()
        {
            var hours = _last2WeeksAvgTemp switch
            {
                null => (min: 4, max: 6),
                < -10 => (min: 4, max: 6),
                < 0 => (min: 4, max: 6),
                < 5 => (min: 4, max: 5),
                < 10 => (min: 4, max: 5),
                < 15 => (min: 3, max: 5),
                < 20 => (min: 2, max: 3),
                _ => (min: 2, max: 3)
            };
            _logger.LogInformation("{service}:: Day target hours: {min}-{max}h (2-week avg: {temp}°C)",
                _serviceName, hours.min, hours.max, _last2WeeksAvgTemp?.ToString("F1") ?? "N/A");
            return hours;
        }

        private LowPriceDateTimeRange GetBestNightPeriod()
        {
            var nightWindowStart = DateTime.Today.AddHours(22);          // 22:00 today
            var nightWindowEnd = DateTime.Today.AddDays(1).AddHours(8); // 08:00 tomorrow
            var (min, max) = GetNightTargetHours();
            return GetPeriod(nightWindowStart, nightWindowEnd, minTargetHours: min, maxTargetHours: max);
        }

        private LowPriceDateTimeRange GetBestDayPeriod()
        {
            var useTomorrow = TomorrowPrices.Count > 0 && DateTime.Now.Hour >= 12;
            var baseDate = useTomorrow ? DateTime.Today.AddDays(1) : DateTime.Today;

            var dayWindowStart = baseDate.AddHours(8);  // 08:00
            var dayWindowEnd = baseDate.AddHours(22);   // 22:00

            if (useTomorrow)
            {
                _logger.LogInformation("{service}:: Calculating day period for tomorrow ({date:yyyy-MM-dd})", _serviceName, baseDate);
            }

            var (min, max) = GetDayTargetHours();
            return GetPeriod(dayWindowStart, dayWindowEnd, minTargetHours: min, maxTargetHours: max);
        }

        private LowPriceDateTimeRange GetPeriod(DateTime windowStart, DateTime windowEnd, int minTargetHours, int maxTargetHours)
        {
            var windowHours = (windowEnd - windowStart).TotalHours;
            var defaultPeriod = new LowPriceDateTimeRange
            {
                Start = windowStart,
                End = windowStart.AddHours(Math.Min(maxTargetHours, windowHours)),
                AveragePrice = 0,
                Rank = 0
            };

            bool spansMidnight = windowEnd.Date > windowStart.Date;
            bool isTomorrowWindow = windowStart.Date > DateTime.Today;

            if (TodayPrices.Count == 0 && !isTomorrowWindow)
            {
                _logger.LogWarning("{service}:: Insufficient price data for period {start:HH:mm}-{end:HH:mm} calculation",
                    _serviceName, windowStart, windowEnd);
                return defaultPeriod;
            }

            if ((spansMidnight || isTomorrowWindow) && TomorrowPrices.Count == 0)
            {
                _logger.LogWarning("{service}:: Insufficient price data for period {start:HH:mm}-{end:HH:mm} calculation - need tomorrow prices",
                    _serviceName, windowStart, windowEnd);
                return defaultPeriod;
            }

            var combinedPrices = new List<ParsedPrice>();

            foreach (var price in TodayPrices)
            {
                if (DateTime.TryParseExact(price.date, GlobalConst.PriceTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
                    && dt >= windowStart && dt < windowEnd)
                {
                    TryAddPrice(price, combinedPrices);
                }
            }

            if (spansMidnight || isTomorrowWindow)
            {
                foreach (var price in TomorrowPrices)
                {
                    if (DateTime.TryParseExact(price.date, GlobalConst.PriceTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
                        && dt >= windowStart && dt < windowEnd)
                    {
                        TryAddPrice(price, combinedPrices);
                    }
                }
            }

            var minSlotsRequired = minTargetHours * 4;
            if (combinedPrices.Count < minSlotsRequired)
            {
                _logger.LogWarning("{service}:: Insufficient price data for period {start:HH:mm}-{end:HH:mm} (got {count} slots, need ≥{min}). Returning default.",
                    _serviceName, windowStart, windowEnd, combinedPrices.Count, minSlotsRequired);
                return defaultPeriod;
            }

            combinedPrices = [.. combinedPrices.OrderBy(p => p.DateTime)];

            _logger.LogInformation("{service}:: Period {start:HH:mm}-{end:HH:mm} slot span {first:yyyy-MM-dd HH:mm} -> {last:yyyy-MM-dd HH:mm} ({count} slots)",
                _serviceName, windowStart, windowEnd, combinedPrices.First().DateTime, combinedPrices.Last().DateTime, combinedPrices.Count);

            return FindOptimalPeriod(combinedPrices, minTargetHours, maxTargetHours) ?? defaultPeriod;
        }

        private LowPriceDateTimeRange? FindOptimalPeriod(List<ParsedPrice> prices, int minTargetHours, int maxTargetHours)
        {
            var candidates = new List<LowPriceDateTimeRange>();

            for (int targetHours = minTargetHours; targetHours <= maxTargetHours; targetHours++)
            {
                int slotsNeeded = targetHours * 4;
                if (prices.Count < slotsNeeded)
                    break;

                for (int startIndex = 0; startIndex <= prices.Count - slotsNeeded; startIndex++)
                {
                    var window = prices.Skip(startIndex).Take(slotsNeeded).ToList();
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
                _logger.LogWarning($"{_serviceName}:: No continuous period candidates found");
                return null;
            }

            var best = candidates
                .OrderBy(c => c.AveragePrice)
                .ThenByDescending(c => TimeUtils.GetHoursInRange(c))
                .ThenBy(c => c.Start)
                .First();

            best.Rank = 1;

            _logger.LogInformation($"{_serviceName}:: Best period: {best.Start:HH:mm}-{best.End:HH:mm} " +
                                   $"({TimeUtils.GetHoursInRange(best):F1}h) avg {best.AveragePrice:F4}");

            return best;
        }

        private static bool IsContinuousPeriod(List<ParsedPrice> slots)
        {
            if (slots == null || slots.Count == 0)
                return false;

            var ordered = slots.OrderBy(s => s.DateTime).ToList();

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
