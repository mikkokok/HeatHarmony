using HeatHarmony.Models;

namespace HeatHarmony.DTO
{
    public sealed class PriceTodayResponse
    {
        public required IReadOnlyList<ElectricityPrice> Prices { get; set; }
    }
}