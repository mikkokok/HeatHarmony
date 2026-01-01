using HeatHarmony.DTO;
using HeatHarmony.Models;
using HeatHarmony.Providers;
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
                var response = new HeatAutomationStatusResponse
                {
                    IsWorkerRunning = provider.IsWorkerRunning,
                    ServerTime = DateTime.Now
                };

                return Results.Ok(response);
            })
            .WithName("GetHeatAutomationStatus")
            .Produces<HeatAutomationStatusResponse>(StatusCodes.Status200OK);

            heat.MapGet("/tasks", ([FromServices] HeatAutomationWorkerProvider provider) =>
            {
                string TaskStatus(Task? t) => t?.Status.ToString() ?? "NotStarted";
                string[] TaskErrors(Task? t)
                    => (t?.Exception?.Message is { } msg) ? [msg] : [];

                var response = new HeatAutomationTasksResponse
                {
                    OumanAndHeishamonSync = new HeatAutomationTaskDetails
                    {
                        Status = TaskStatus(provider.OumanAndHeishamonSyncTask),
                        Errors = TaskErrors(provider.OumanAndHeishamonSyncTask)
                    },
                    SetUseWaterBasedOnPrice = new HeatAutomationTaskDetails
                    {
                        Status = TaskStatus(provider.SetUseWaterBasedOnPriceTask),
                        Errors = TaskErrors(provider.SetUseWaterBasedOnPriceTask)
                    },
                    SetInsideTempBasedOnPrice = new HeatAutomationTaskDetails
                    {
                        Status = TaskStatus(provider.SetInsideTempBasedOnPriceTask),
                        Errors = TaskErrors(provider.SetInsideTempBasedOnPriceTask)
                    },
                    ServerTime = DateTime.Now
                };

                return Results.Ok(response);
            })
            .WithName("GetHeatAutomationTaskStatus")
            .Produces<HeatAutomationTasksResponse>(StatusCodes.Status200OK);

            heat.MapGet("/override", ([FromServices] HeatAutomationWorkerProvider provider) =>
            {
                var response = new HeatAutomationOverrideStatusResponse
                {
                    IsActive = provider.overRide,
                    TargetTemp = provider.overRideTemp,
                    Until = provider.overRideUntil,
                    ServerTime = DateTime.Now
                };

                return Results.Ok(response);
            })
            .WithName("GetOverrideStatus")
            .Produces<HeatAutomationOverrideStatusResponse>(StatusCodes.Status200OK);

            heat.MapPost("/override", ([FromServices] HeatAutomationWorkerProvider provider, [FromBody] TemperatureOverride request) =>
            {
                if (request.Hours <= 0 || request.Hours > 48)
                    return Results.BadRequest(new ErrorResponse { Message = "Hours must be between 1 and 48." });

                if (request.Temperature < 10 || request.Temperature > 30)
                    return Results.BadRequest(new ErrorResponse { Message = "Temperature must be between 10°C and 30°C." });

                if (!request.OverRidePrevious && provider.overRide)
                    return Results.Conflict(new ErrorResponse { Message = "Override already in progress." });

                var delay = request.Delay < 0 ? 0 : request.Delay;

                provider.OverRideTemp(request.Hours, request.Temperature, request.OverRidePrevious, delay);

                var response = new HeatAutomationOverrideAcceptedResponse
                {
                    Message = "Override scheduled",
                    Temperature = request.Temperature,
                    Hours = request.Hours,
                    DelayHours = delay,
                    RequestedAt = DateTime.Now
                };

                return Results.Accepted(value: response);
            })
            .WithName("SetOverrideTemp")
            .Produces<HeatAutomationOverrideAcceptedResponse>(StatusCodes.Status202Accepted)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict);

            heat.MapDelete("/override", ([FromServices] HeatAutomationWorkerProvider provider) =>
            {
                if (!provider.overRide)
                    return Results.Conflict(new ErrorResponse { Message = "No override in progress." });

                provider.CancelOverRide();

                var response = new HeatAutomationOverrideCancelledResponse
                {
                    Message = "Override cancelled.",
                    CancelledAt = DateTime.Now
                };

                return Results.Ok(response);
            })
            .WithName("CancelOverrideTemp")
            .Produces<HeatAutomationOverrideCancelledResponse>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict);
        }
    }
}
