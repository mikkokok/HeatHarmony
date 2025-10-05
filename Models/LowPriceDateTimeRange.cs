namespace HeatHarmony.Models
{
    public sealed class LowPriceDateTimeRange()
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public int Rank { get; set; }
        public decimal AveragePrice { get; set; }
    }
}
