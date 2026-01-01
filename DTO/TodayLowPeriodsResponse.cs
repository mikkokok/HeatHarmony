using HeatHarmony.Models;

namespace HeatHarmony.DTO
{
    public sealed class TodayLowPeriodsResponse
    {
        public required IReadOnlyList<LowPriceDateTimeRange> Periods { get; set; }
    }
}