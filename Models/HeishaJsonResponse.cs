using Newtonsoft.Json;

namespace HeatHarmony.Models
{
#pragma warning disable IDE1006 // Naming Styles
    public class Heatpump
    {
        public required string Topic { get; set; }
        public required string Name { get; set; }
        public required string Value { get; set; }
        public required string Description { get; set; }
    }

    public class HeishaJsonResponse
    {
        public required List<Heatpump> heatpump { get; set; }

        [JsonProperty("1wire")]
        public required List<object> _1wire { get; set; }
        public required List<S0> s0 { get; set; }
    }

    public class S0
    {
        [JsonProperty("S0 port")]
        public required string S0port { get; set; }
        public required string Watt { get; set; }
        public required string Watthour { get; set; }
        public required string WatthourTotal { get; set; }
        public required string PulseQuality { get; set; }
        public required string AvgPulseWidth { get; set; }
    }
}
