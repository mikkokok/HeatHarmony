using HeatHarmony.Models;

namespace HeatHarmony.DTO
{
    public sealed class PriceTomorrowResponse
    {
        public required IReadOnlyList<ElectricityPrice> Prices { get; set; }
    }
}