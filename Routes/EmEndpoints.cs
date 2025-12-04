using HeatHarmony.Models;
using HeatHarmony.Providers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
                return Results.Ok(new
                {
                    emProvider.LastEnabled,
                    emProvider.IsOverridden,
                    isRunning,
                    emProvider.IsOn
                });
            })
            .WithName("GetLatestEM")
            .Produces(StatusCodes.Status200OK);

            em.MapGet("/changes", ([FromServices] EMProvider emProvider) =>
            {
                return Results.Ok(emProvider.Changes);
            })
            .WithName("GetEMChanges")
            .Produces<List<HarmonyChange>>(StatusCodes.Status200OK);

            em.MapPut("/enable", async ([FromServices] EMProvider emProvider) =>
            {
                await emProvider.EnableWaterHeating();
                return Results.Accepted();
            })
            .WithName("EnableEMWaterHeating")
            .Produces(StatusCodes.Status202Accepted);

            em.MapPut("/disable", async ([FromServices] EMProvider emProvider) =>
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

            em.MapPut("/override/enable/{hours?}", async ([FromServices] EMProvider emProvider, int? hours) =>
            {
                if (hours is int h && h <= 0) return Results.BadRequest(new { message = "hours must be > 0" });
                await emProvider.ApplyOverride(EMOverrideMode.Enable, hours);
                return Results.Accepted(value: new { mode = EMOverrideMode.Enable, hours });
            })
            .WithName("OverrideEMEnableWaterHeating")
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status400BadRequest);

            em.MapPut("/override/disable/{hours?}", async ([FromServices] EMProvider emProvider, int? hours) =>
            {
                if (hours is int h && h <= 0) return Results.BadRequest(new { message = "hours must be > 0" });
                await emProvider.ApplyOverride(EMOverrideMode.Disable, hours);
                return Results.Accepted(value: new { mode = EMOverrideMode.Disable, hours });
            })
            .WithName("OverrideEMDisableWaterHeating")
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status400BadRequest);

            em.MapGet("/override/status", ([FromServices] EMProvider emProvider) =>
            {
                return Results.Ok(new
                {
                    emProvider.OverrideMode,
                    emProvider.IsOverrideActive,
                    emProvider.OverrideUntil
                });
            })
            .WithName("GetEMOverrideStatus")
            .Produces(StatusCodes.Status200OK);
        }
    }
}
