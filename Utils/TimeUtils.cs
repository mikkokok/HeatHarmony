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
            var today = DateTime.Now;
            return time.Year == today.Year && time.Month == today.Month && time.Day == today.Day;
        }
        public static bool HasBeenLongerThan(DateTime time, int hours)
        {
            DateTime now = DateTime.Now;
            return time <= now.AddHours(-hours);
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

        public static decimal? GetCurrentTimePrice(List<LowPriceDateTimeRange> periods)
        {
            return periods.FirstOrDefault(period => IsCurrentTimeInRange(period))?.AveragePrice;
        }

        public static DateTime GetDateTimeInMidnight()
        {
            var now = DateTime.Now;
            return new DateTime(now.Year, now.Month, now.Day, 0, 0, 0);
        }

        public static double HoursSince(DateTime time)
        {
            var now = DateTime.Now;
            if (time == DateTime.MinValue) return double.MaxValue;
            return (now - time).TotalHours;
        }
    }
}
