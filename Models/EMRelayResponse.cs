using System.Text.Json.Serialization;

namespace HeatHarmony.Models
{
    public sealed class EMRelayResponse
    {
        [JsonPropertyName("ison")]
        public bool IsOn { get; set; }

        [JsonPropertyName("has_timer")]
        public bool HasTimer { get; set; }

        [JsonPropertyName("timer_started")]
        public int TimerStarted { get; set; }

        [JsonPropertyName("timer_duration")]
        public int TimerDuration { get; set; }

        [JsonPropertyName("timer_remaining")]
        public int TimerRemaining { get; set; }

        [JsonPropertyName("overpower")]
        public bool Overpower { get; set; }

        [JsonPropertyName("is_valid")]
        public bool IsValid { get; set; }

        [JsonPropertyName("source")]
        public required string Source { get; set; }
    }
}
