using HeatHarmony.Models;

namespace HeatHarmony.Helpers
{
    public interface IHeatPoller
    {
        List<AirWaterHeatPumpUpdate> Updates { get; }
    }
}
