using HeatHarmony.Models;
using HeatHarmony.Providers;
using Microsoft.AspNetCore.Mvc;

namespace HeatHarmony.Routes
{
    public partial class ApiMapper
    {
        public static void MapHeatAutomationEndpoints(this WebApplication app)
        {
            var heatAutomationEndpoints = app.MapGroup("/heatautomation").WithTags("HeatAutomationEndpoints");
            heatAutomationEndpoints.MapGet("/status", ([FromServices] HeatAutomationWorkerProvider heatAutomationWorkerProvider) =>
            {
                return Results.Ok(new { status = heatAutomationWorkerProvider.IsWorkerRunning });
            }).WithName("GetHeatAutomationStatus");
            heatAutomationEndpoints.MapGet("/tasks", ([FromServices] HeatAutomationWorkerProvider heatAutomationWorkerProvider) =>
            {
                return Results.Ok(new
                {
                    SetInsideTempBasedOnPriceTask = heatAutomationWorkerProvider.SetInsideTempBasedOnPriceTask?.Status.ToString() ?? "No SetHeatBasedOnPrice task",
                    SetInsideTempBasedOnPriceTaskExceptions = heatAutomationWorkerProvider.SetInsideTempBasedOnPriceTask?.Exception?.Message != null
                        ? [heatAutomationWorkerProvider.SetInsideTempBasedOnPriceTask.Exception.Message]
                        : Array.Empty<string>(),
                    SetUseWaterBasedOnPriceTask = heatAutomationWorkerProvider.SetUseWaterBasedOnPriceTask?.Status.ToString() ?? "No SetHeatBasedOnPrice task",
                    SetUseWaterBasedOnPriceTaskExceptions = heatAutomationWorkerProvider.SetUseWaterBasedOnPriceTask?.Exception?.Message != null
                        ? [heatAutomationWorkerProvider.SetUseWaterBasedOnPriceTask.Exception.Message]
                        : Array.Empty<string>(),
                    OumanAndHeishamonSyncTask = heatAutomationWorkerProvider.OumanAndHeishamonSyncTask?.Status.ToString() ?? "No OumanAndHeishamonSync task",
                    OumanAndHeishamonSyncTaskExceptions = heatAutomationWorkerProvider.OumanAndHeishamonSyncTask?.Exception?.Message != null
                        ? [heatAutomationWorkerProvider.OumanAndHeishamonSyncTask.Exception.Message]
                        : Array.Empty<string>()

                });
            }).WithName("GetHeatAutomationTaskStatus");
            heatAutomationEndpoints.MapGet("/override", ([FromServices] HeatAutomationWorkerProvider heatAutomationWorkerProvider) =>
            {
                return Results.Ok(new { heatAutomationWorkerProvider.overRide, heatAutomationWorkerProvider.overRideTemp });
            }).WithName("GetOverrideStatus");
            heatAutomationEndpoints.MapPost("/override", ([FromServices] HeatAutomationWorkerProvider heatAutomationWorkerProvider, [FromBody] TemperatureOverride temperatureOverride) =>
            {
                if (!temperatureOverride.OverRidePrevious && heatAutomationWorkerProvider.overRide)
                {
                    return Results.Conflict(new { message = "Override already in progress" });
                }
                heatAutomationWorkerProvider.OverRideTemp(temperatureOverride.Hours, temperatureOverride.Temperature, temperatureOverride.OverRidePrevious);
                return Results.Ok(new { message = $"Override set to {temperatureOverride.Temperature}°C for {temperatureOverride.Hours} hours" });
            }).WithName("SetOverrideTemp");
            heatAutomationEndpoints.MapDelete("/override", ([FromServices] HeatAutomationWorkerProvider heatAutomationWorkerProvider) =>
            {
                if (!heatAutomationWorkerProvider.overRide)
                {
                    return Results.Conflict(new { message = "No override in progress" });
                }
                heatAutomationWorkerProvider.CancelOverRide();
                return Results.Ok(new { message = "Override cancelled" });
            }).WithName("CancelOverrideTemp");
        }
    }
}
