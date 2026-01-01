using HeatHarmony.Models;

namespace HeatHarmony.DTO
{
    public sealed class HeishaMonStatusResponse
    {
        public required IReadOnlyList<HarmonyChange> Changes { get; set; }
        public required DateTime ServerTime { get; set; }
    }
}