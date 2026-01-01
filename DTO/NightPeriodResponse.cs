using HeatHarmony.Models;

namespace HeatHarmony.DTO
{
    public sealed class NightPeriodResponse
    {
        public required LowPriceDateTimeRange Period { get; set; }
    }
}