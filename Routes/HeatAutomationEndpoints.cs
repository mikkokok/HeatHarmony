using HeatHarmony.Models;
using HeatHarmony.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HeatHarmony.Routes
{
    public partial class ApiMapper
    {
        public static void MapHeatAutomationEndpoints(this WebApplication app)
        {
            var heat = app.MapGroup("/heatautomation")
                          .WithTags("HeatAutomationEndpoints");

            heat.MapGet("/status", ([FromServices] HeatAutomationWorkerProvider provider) =>
            {
                return Results.Ok(new
                {
                    isWorkerRunning = provider.IsWorkerRunning,
                    serverTime = DateTime.Now
                });
            })
            .WithName("GetHeatAutomationStatus")
            .Produces(StatusCodes.Status200OK);

            heat.MapGet("/tasks", ([FromServices] HeatAutomationWorkerProvider provider) =>
            {
                string TaskStatus(Task? t) => t?.Status.ToString() ?? "NotStarted";
                string[] TaskErrors(Task? t)
                    => (t?.Exception?.Message is { } msg) ? new[] { msg } : Array.Empty<string>();

                return Results.Ok(new
                {
                    oumanAndHeishamonSync = new
                    {
                        status = TaskStatus(provider.OumanAndHeishamonSyncTask),
                        errors = TaskErrors(provider.OumanAndHeishamonSyncTask)
                    },
                    setUseWaterBasedOnPrice = new
                    {
                        status = TaskStatus(provider.SetUseWaterBasedOnPriceTask),
                        errors = TaskErrors(provider.SetUseWaterBasedOnPriceTask)
                    },
                    setInsideTempBasedOnPrice = new
                    {
                        status = TaskStatus(provider.SetInsideTempBasedOnPriceTask),
                        errors = TaskErrors(provider.SetInsideTempBasedOnPriceTask)
                    },
                    serverTime = DateTime.Now
                });
            })
            .WithName("GetHeatAutomationTaskStatus")
            .Produces(StatusCodes.Status200OK);

            heat.MapGet("/override", ([FromServices] HeatAutomationWorkerProvider provider) =>
            {
                return Results.Ok(new
                {
                    isActive = provider.overRide,
                    targetTemp = provider.overRideTemp,
                    until = provider.overRideUntil,
                    serverTime = DateTime.Now
                });
            })
            .WithName("GetOverrideStatus")
            .Produces(StatusCodes.Status200OK);

            heat.MapPost("/override", ([FromServices] HeatAutomationWorkerProvider provider, [FromBody] TemperatureOverride request) =>
            {
                if (request.Hours <= 0 || request.Hours > 48)
                    return Results.BadRequest(new { message = "Hours must be between 1 and 48." });

                if (request.Temperature < 10 || request.Temperature > 30)
                    return Results.BadRequest(new { message = "Temperature must be between 10°C and 30°C." });

                if (!request.OverRidePrevious && provider.overRide)
                    return Results.Conflict(new { message = "Override already in progress." });

                var delay = request.Delay < 0 ? 0 : request.Delay;

                provider.OverRideTemp(request.Hours, request.Temperature, request.OverRidePrevious, delay);

                return Results.Accepted(value: new
                {
                    message = "Override scheduled",
                    temperature = request.Temperature,
                    hours = request.Hours,
                    delayHours = delay,
                    requestedAt = DateTime.Now
                });
            })
            .WithName("SetOverrideTemp")
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict);

            heat.MapDelete("/override", ([FromServices] HeatAutomationWorkerProvider provider) =>
            {
                if (!provider.overRide)
                    return Results.Conflict(new { message = "No override in progress." });

                provider.CancelOverRide();
                return Results.Ok(new { message = "Override cancelled.", cancelledAt = DateTime.Now });
            })
            .WithName("CancelOverrideTemp")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status409Conflict);
        }
    }
}
