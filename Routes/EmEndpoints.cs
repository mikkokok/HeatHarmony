using HeatHarmony.DTO;
using HeatHarmony.Models;
using HeatHarmony.Providers;
using Microsoft.AspNetCore.Mvc;

namespace HeatHarmony.Routes
{
    public static partial class ApiMapper
    {
        public static void MapEmEndPoints(this WebApplication app)
        {
            var em = app.MapGroup("/em")
                        .WithTags("EmEndpoints");

            em.MapGet("/latest", async ([FromServices] EMProvider emProvider) =>
            {
                var isRunning = await emProvider.IsRunning();

                var response = new EmLatestResponse
                {
                    LastEnabled = emProvider.LastEnabled,
                    IsOverridden = emProvider.IsOverridden,
                    IsRunning = isRunning,
                    IsOn = emProvider.IsOn
                };

                return Results.Ok(response);
            })
            .WithName("GetLatestEM")
            .Produces<EmLatestResponse>(StatusCodes.Status200OK);

            em.MapGet("/changes", ([FromServices] EMProvider emProvider) =>
            {
                var response = new EmChangesResponse
                {
                    Changes = emProvider.Changes
                };

                return Results.Ok(response);
            })
            .WithName("GetEMChanges")
            .Produces<EmChangesResponse>(StatusCodes.Status200OK);

            em.MapPost("/enable", async ([FromServices] EMProvider emProvider) =>
            {
                await emProvider.EnableWaterHeating();
                return Results.Accepted();
            })
            .WithName("EnableEMWaterHeating")
            .Produces(StatusCodes.Status202Accepted);

            em.MapPost("/disable", async ([FromServices] EMProvider emProvider) =>
            {
                await emProvider.DisableWaterHeating();
                return Results.Accepted();
            })
            .WithName("DisableEMWaterHeating")
            .Produces(StatusCodes.Status202Accepted);

            em.MapDelete("/override/delete", async ([FromServices] EMProvider emProvider) =>
            {
                await emProvider.ApplyOverride(EMOverrideMode.None, 0);
                return Results.Ok();
            })
            .WithName("ClearEMOverride")
            .Produces(StatusCodes.Status200OK);

            em.MapPost("/override/enable/{hours?}", async ([FromServices] EMProvider emProvider, int? hours) =>
            {
                if (hours is int h && h <= 0)
                    return Results.BadRequest(new ErrorResponse { Message = "hours must be > 0" });

                await emProvider.ApplyOverride(EMOverrideMode.Enable, hours);

                var response = new EmOverrideResultResponse
                {
                    Mode = EMOverrideMode.Enable,
                    Hours = hours
                };

                return Results.Accepted(value: response);
            })
            .WithName("OverrideEMEnableWaterHeating")
            .Produces<EmOverrideResultResponse>(StatusCodes.Status202Accepted)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

            em.MapPost("/override/disable/{hours?}", async ([FromServices] EMProvider emProvider, int? hours) =>
            {
                if (hours is int h && h <= 0)
                    return Results.BadRequest(new ErrorResponse { Message = "hours must be > 0" });

                await emProvider.ApplyOverride(EMOverrideMode.Disable, hours);

                var response = new EmOverrideResultResponse
                {
                    Mode = EMOverrideMode.Disable,
                    Hours = hours
                };

                return Results.Accepted(value: response);
            })
            .WithName("OverrideEMDisableWaterHeating")
            .Produces<EmOverrideResultResponse>(StatusCodes.Status202Accepted)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

            em.MapGet("/override/status", ([FromServices] EMProvider emProvider) =>
            {
                var response = new EmOverrideStatusResponse
                {
                    OverrideMode = emProvider.OverrideMode,
                    IsOverrideActive = emProvider.IsOverrideActive,
                    OverrideUntil = emProvider.OverrideUntil
                };

                return Results.Ok(response);
            })
            .WithName("GetEMOverrideStatus")
            .Produces<EmOverrideStatusResponse>(StatusCodes.Status200OK);
        }
    }
}
