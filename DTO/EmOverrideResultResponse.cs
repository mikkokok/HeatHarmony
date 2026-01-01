using HeatHarmony.Models;

namespace HeatHarmony.DTO
{
    public sealed class EmOverrideResultResponse
    {
        public EMOverrideMode Mode { get; set; }
        public int? Hours { get; set; }
    }
}