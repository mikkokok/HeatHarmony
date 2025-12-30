namespace HeatHarmony.Models
{
#pragma warning disable IDE1006 // Naming Styles
    public class PMProStatusResponse
    {
        public bool ison { get; set; }
        public bool has_timer { get; set; }
        public int timer_started_at { get; set; }
        public double timer_duration { get; set; }
        public double timer_remaining { get; set; }
        public bool overpower { get; set; }
        public required string source { get; set; }
    }
}
