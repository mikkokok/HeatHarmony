using HeatHarmony.Models;

namespace HeatHarmony.DTO
{
    public sealed class HeatingPeriodResponse
    {
        public required LowPriceDateTimeRange Period { get; set; }
    }
}