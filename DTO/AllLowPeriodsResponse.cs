using HeatHarmony.Models;

namespace HeatHarmony.DTO
{
    public sealed class AllLowPeriodsResponse
    {
        public required IReadOnlyList<LowPriceDateTimeRange> Periods { get; set; }
    }
}