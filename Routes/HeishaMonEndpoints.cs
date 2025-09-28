using HeatHarmony.Providers;
using Microsoft.AspNetCore.Mvc;

namespace HeatHarmony.Routes
{
    public static partial class ApiMapper
    {
        public static void MapHeishaMonEndpoints(this WebApplication app)
        {
            var heishaMonEndpoints = app.MapGroup("/heishamon").WithTags("HeishaMonEndpoints");
            heishaMonEndpoints.MapGet("/latest", ([FromServices] HeishaMonProvider heishaMonProvider) =>
            {
                return Results.Ok(new { heishaMonProvider.MainInletTemp, heishaMonProvider.MainOutletTemp, heishaMonProvider.MainTargetTemp});
            }).WithName("GetLatestHeishaMonReadings");
            heishaMonEndpoints.MapGet("/task", ([FromServices] HeishaMonProvider heishaMonProvider) =>
            {
                return Results.Ok(heishaMonProvider.HeishaMonTask.Status.ToString());
            }).WithName("GetHeishaMonProviderTask");
        }
    }
}
