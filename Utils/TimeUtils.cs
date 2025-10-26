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
        public static bool IsTimeWithinHourRange(DateTime? time, int hours)
        {
            if (time == null)
            {
                return false;
            }
            DateTime now = DateTime.Now;
            return time > now.AddHours(-24) && time <= now;
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
    }
}
