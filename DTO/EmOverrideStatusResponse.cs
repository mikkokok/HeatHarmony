using HeatHarmony.Models;

namespace HeatHarmony.DTO
{
    public sealed class EmOverrideStatusResponse
    {
        public EMOverrideMode OverrideMode { get; set; }
        public bool IsOverrideActive { get; set; }
        public DateTime? OverrideUntil { get; set; }
    }
}