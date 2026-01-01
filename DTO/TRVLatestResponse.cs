using static HeatHarmony.Config.GlobalConfig;

namespace HeatHarmony.DTO
{
    public sealed class TRVLatestResponse
    {
        public required List<ShellyTRV> Devices { get; set; }
        public required DateTime ServerTime { get; set; }
    }
}
