using HeatHarmony.Models;
using HeatHarmony.Providers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;

namespace HeatHarmony.Routes
{
    public static partial class ApiMapper
    {
        public static void MapEmEndPoints(this WebApplication app)
        {
            var emEndpoints = app.MapGroup("/em").WithTags("EmEndpoints");
            emEndpoints.MapGet("/latest", ([FromServices] EMProvider eMProvider) =>
            {
                return Results.Ok(new { eMProvider.LastEnabled, eMProvider.IsOverridden, isRunning = eMProvider.IsRunning(), eMProvider.IsOn });
            }).WithName("GetLatestEM");
            emEndpoints.MapGet("/changes", ([FromServices] EMProvider eMProvider) =>
            {
                return Results.Ok(eMProvider.Changes);
            }).WithName("GetEMChanges");

            emEndpoints.MapPut("/enable", async ([FromServices] EMProvider eMProvider) =>
                        {
                            await eMProvider.EnableWaterHeating();
                            return Results.Ok();
                        }).WithName("EnableEMWaterHeating");
            emEndpoints.MapPut("/disable", async ([FromServices] EMProvider eMProvider) =>
            {
                await eMProvider.DisableWaterHeating();
                return Results.Ok();
            }).WithName("DisableEMWaterHeating");
            emEndpoints.MapDelete("/override/delete", async ([FromServices] EMProvider eMProvider) =>
            {
                await eMProvider.ApplyOverride(EMOverrideMode.None, 0);
                return Results.Ok();
            }).WithName("ClearEMOverride");
            emEndpoints.MapPut("/override/enable/{hours?}", async ([FromServices] EMProvider eMProvider, int? hours) =>
            {
                await eMProvider.ApplyOverride(EMOverrideMode.Enable, hours);
                return Results.Ok();
            }).WithName("OverrideEMEnableWaterHeating");
            emEndpoints.MapPut("/override/disable/{hours?}", async ([FromServices] EMProvider eMProvider, int? hours) =>
            {
                await eMProvider.ApplyOverride(EMOverrideMode.Disable, hours);
                return Results.Ok();
            }).WithName("OverrideEMDisableWaterHeating");
            emEndpoints.MapGet("/override/status", ([FromServices] EMProvider eMProvider) =>
            {
                return Results.Ok(new { eMProvider.OverrideMode, eMProvider.IsOverrideActive, eMProvider.OverrideUntil });
            }).WithName("GetEMOverrideStatus");
        }
    }
}
