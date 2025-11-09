using HeatHarmony.Config;
using HeatHarmony.Models;
using System;

namespace HeatHarmony.Utils
{
    public static class TimeUtils
    {
        public static double GetHoursInRange(LowPriceDateTimeRange range)
        {
            return (range.End - range.Start).TotalHours;
        }
        public static bool IsTimeToday(DateTime time)
        {
            var today = DateTime.UtcNow;
            return time.Year == today.Year && time.Month == today.Month && time.Day == today.Day;
        }
        public static bool IsTimeWithinHourRange(DateTime time, int hours)
        {
            DateTime now = DateTime.Now;
            return time > now.AddHours(hours) && time <= now;
        }
        public static bool IsCurrentTimeInRange(LowPriceDateTimeRange? range)
        {
            if (range == null)
            {
                return false;
            }
            DateTime now = DateTime.Now;
            return now >= range.Start && now <= range.End;
        }
        public static double HowManyHoursUntil(DateTime? time)
        {
            if (time == null)
            {
                return 0;
            }
            var now = DateTime.Now;
            if (time <= now)
            {
                return 0;
            }
            return (time.Value - now).TotalHours;
        }

        public static bool IsSequentialRanks(LowPriceDateTimeRange range1, LowPriceDateTimeRange range2)
        {
            return range1.End == range2.Start || range2.End == range1.Start;
        }

        public static int HowManyRanksUntilHours(double hours, List<LowPriceDateTimeRange> rankedPeriods)
        {
            int ranks = 0;
            double accumulatedHours = 0.0;
            foreach (var period in rankedPeriods)
            {
                accumulatedHours += GetHoursInRange(period);
                ranks++;
                if (accumulatedHours >= hours)
                {
                    break;
                }
            }
            return ranks;
        }

        public static bool AllSequentialRanksExists(List<LowPriceDateTimeRange> rankedPeriods)
        {
            for (int i = 0; i < rankedPeriods.Count - 1; i++)
            {
                if (!(Math.Abs(rankedPeriods[i].Rank - rankedPeriods[i + 1].Rank) == 1))
                {
                    return false;
                }
            }
            return true;
        }

        public static decimal? GetCurrentTimePrice(List<LowPriceDateTimeRange> periods)
        {
            return periods.FirstOrDefault(period => IsCurrentTimeInRange(period))?.AveragePrice;
        }

        public static DateTime GetDateTimeInMidnight()
        {
            var now = DateTime.Now;
            return new DateTime(now.Year, now.Month, now.Day, 0, 0, 0);
        }
    }
}
