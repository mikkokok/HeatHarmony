using HeatHarmony.Providers;
using Microsoft.AspNetCore.Mvc;

namespace HeatHarmony.Routes
{
    public static partial class ApiMapper
    {
        public static void MapOumanEndPoints(this WebApplication app)
        {
            var oumanEndpoints = app.MapGroup("/ouman").WithTags("OumanEndPoints");
            oumanEndpoints.MapGet("/latest", ([FromServices] OumanProvider oumanProvider) =>
            {
                return Results.Ok(new { oumanProvider.LatestOutsideTemp, oumanProvider.LatestFlowDemand, oumanProvider.LatestInsideTempDemand, oumanProvider.LatestMinFlowTemp, oumanProvider.AutoTemp });
            }).WithName("GetLatestOumanReadings");
            oumanEndpoints.MapGet("/task", ([FromServices] OumanProvider oumanProvider) =>
            {
                return Results.Ok(oumanProvider.OumanTask);
            }).WithName("GetOumanProviderTask");
        }
    }
}
