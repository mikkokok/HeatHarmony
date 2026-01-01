using HeatHarmony.DTO;
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
                var response = new OumanLatestResponse
                {
                    OutsideTemp = provider.LatestOutsideTemp,
                    FlowDemand = provider.LatestFlowDemand,
                    InsideTempDemand = provider.LatestInsideTempDemand,
                    MinFlowTemp = provider.LatestMinFlowTemp,
                    AutoTemp = provider.AutoTemp,
                    InsideTemp = provider.LatestInsideTemp,
                    ServerTime = DateTime.Now
                };

                return Results.Ok(response);
            })
            .WithName("GetLatestOumanReadings")
            .Produces<OumanLatestResponse>(StatusCodes.Status200OK);

            ouman.MapGet("/status", ([FromServices] OumanProvider provider) =>
            {
                var response = new OumanStatusResponse
                {
                    Changes = provider.Changes,
                    ServerTime = DateTime.Now
                };

                return Results.Ok(response);
            })
            .WithName("GetOumanStatus")
            .Produces<OumanStatusResponse>(StatusCodes.Status200OK);

            ouman.MapGet("/task", ([FromServices] OumanProvider provider) =>
            {
                var status = provider.OumanTask?.Status.ToString() ?? "NotStarted";
                var error = provider.OumanTask?.Exception?.Message;

                var response = new OumanTaskResponse
                {
                    Status = status,
                    Errors = error is null ? Array.Empty<string>() : new[] { error },
                    ServerTime = DateTime.Now
                };

                return Results.Ok(response);
            })
            .WithName("GetOumanProviderTask")
            .Produces<OumanTaskResponse>(StatusCodes.Status200OK);
        }
    }
}
