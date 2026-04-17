namespace HeatHarmony.DTO
{
    public sealed class Pro3OverrideStatusResponse
    {
        public bool IsOverridden { get; set; }
        public DateTime? Until { get; set; }
        public int? OutputAmount { get; set; }
        public bool? OutputState { get; set; }
    }
}
