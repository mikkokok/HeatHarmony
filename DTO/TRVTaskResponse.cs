namespace HeatHarmony.DTO
{
    public sealed class TRVTaskResponse
    {
        public required string Status { get; set; }
        public required IReadOnlyList<string> Errors { get; set; }
        public required DateTime ServerTime { get; set; }
    }
}