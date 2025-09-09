using HeatHarmony.Providers;
using Microsoft.AspNetCore.Mvc;

namespace HeatHarmony.Routes
{
    public static partial class ApiMapper
    {
        public static void MapTRVEndPoints(this WebApplication app)
        {
            var oumanEndpoints = app.MapGroup("/trv").WithTags("TRVEndPoints");
            oumanEndpoints.MapGet("/latest", ([FromServices] TRVProvider trvProvider) =>
            {
                return Results.Ok(trvProvider.GetDevices());
            }).WithName("GetLatestTRVReadings");
            oumanEndpoints.MapGet("/task", ([FromServices] TRVProvider trvProvider) =>
            {
                return Results.Ok(trvProvider.TRVTask);
            }).WithName("GetTRVProviderTask");
        }
    }
}
