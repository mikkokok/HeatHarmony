using HeatHarmony.Models;
using HeatHarmony.Workers;
using Microsoft.AspNetCore.Mvc;

namespace HeatHarmony.Routes
{
    public partial class ApiMapper
    {
        public static void MapHeatAutomationEndpoints(this WebApplication app)
        {
            var heatAutomationEndpoints = app.MapGroup("/heatautomation").WithTags("HeatAutomationEndpoints");
            heatAutomationEndpoints.MapGet("/status", ([FromServices] HeatAutomationWorker heatAutomationWorker) =>
            {
                return Results.Ok(new { heatAutomationWorker.isRunning,});
            }).WithName("GetHeatAutomationStatus");
            heatAutomationEndpoints.MapGet("/override", ([FromServices] HeatAutomationWorker heatAutomationWorker) =>
            {
                return Results.Ok(new {heatAutomationWorker.overRide, heatAutomationWorker.overRideTemp});
            }).WithName("GetHeatAutomationTask");
            heatAutomationEndpoints.MapPost("/override", async ([FromServices] HeatAutomationWorker heatAutomationWorker, [FromBody] TemperatureOverride temperatureOverride) =>
            {
                if (!temperatureOverride.OverRidePrevious && heatAutomationWorker.overRide)
                {
                    return Results.Conflict(new { message = "Override already in progress" });
                }
                await heatAutomationWorker.OverRideTemp(temperatureOverride.Hours, temperatureOverride.Temperature, temperatureOverride.OverRidePrevious);
                return Results.Ok(new { message = $"Override set to {temperatureOverride.Temperature}°C for {temperatureOverride.Hours} hours" });
            }).WithName("SetOverrideTemp");
        }
    }
}
