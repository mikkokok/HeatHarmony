using HeatHarmony.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HeatHarmony.Routes
{
    public static partial class ApiMapper
    {
        public static void MapOumanEndPoints(this WebApplication app)
        {
            var ouman = app.MapGroup("/ouman")
                           .WithTags("OumanEndPoints");

            ouman.MapGet("/latest", ([FromServices] OumanProvider provider) =>
            {
                return Results.Ok(new
                {
                    outsideTemp = provider.LatestOutsideTemp,
                    flowDemand = provider.LatestFlowDemand,
                    insideTempDemand = provider.LatestInsideTempDemand,
                    minFlowTemp = provider.LatestMinFlowTemp,
                    autoTemp = provider.AutoTemp,
                    insideTemp = provider.LatestInsideTemp,
                    serverTime = DateTime.Now
                });
            })
            .WithName("GetLatestOumanReadings")
            .Produces(StatusCodes.Status200OK);

            ouman.MapGet("/status", ([FromServices] OumanProvider provider) =>
            {
                return Results.Ok(new
                {
                    changes = provider.Changes,
                    serverTime = DateTime.Now
                });
            })
            .WithName("GetOumanStatus")
            .Produces(StatusCodes.Status200OK);

            ouman.MapGet("/task", ([FromServices] OumanProvider provider) =>
            {
                var status = provider.OumanTask?.Status.ToString() ?? "NotStarted";
                var error = provider.OumanTask?.Exception?.Message;
                return Results.Ok(new
                {
                    status,
                    errors = error is null ? [] : new[] { error },
                    serverTime = DateTime.Now
                });
            })
            .WithName("GetOumanProviderTask")
            .Produces(StatusCodes.Status200OK);
        }
    }
}
