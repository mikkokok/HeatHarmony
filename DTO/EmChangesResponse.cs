using HeatHarmony.Models;

namespace HeatHarmony.DTO
{
    public sealed class EmChangesResponse
    {
        public required IReadOnlyList<HarmonyChange> Changes { get; set; }
    }
}