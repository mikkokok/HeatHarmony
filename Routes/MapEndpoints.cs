using HeatHarmony.Providers;
using Microsoft.AspNetCore.Mvc;

namespace HeatHarmony.Routes
{
    public static partial class ApiMapper
    {
        public static void MapEndpoints(this WebApplication app)
        {
            var oumanEndpoints = app.MapGroup("/ouman").WithTags("OumanEndpoints");
            oumanEndpoints.MapGet("/latest", ([FromServices]OumanProvider oumanProvider) =>
            {
                return Results.Ok(new { OutsideTemp = oumanProvider.LatestOutsideTemp, FlowTemp = oumanProvider.LatestFlowTemp });
            }).WithName("GetLatestOumanReadings");
        }
    }
}
