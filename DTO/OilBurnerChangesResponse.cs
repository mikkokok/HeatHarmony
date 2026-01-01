using HeatHarmony.Models;

namespace HeatHarmony.DTO
{
    public sealed class OilBurnerChangesResponse
    {
        public required IReadOnlyList<HarmonyChange> Changes { get; set; }
    }
}