using HeatHarmony.Providers;
using Microsoft.AspNetCore.Mvc;

namespace HeatHarmony.Routes
{
    public static partial class ApiMapper
    {
        public static void MapPriceEndPoints(this WebApplication app)
        {
            var priceEndpoints = app.MapGroup("/price").WithTags("PriceEndPoints");
            priceEndpoints.MapGet("/latest", ([FromServices] PriceProvider priceProvider) =>
            {
                return Results.Ok(priceProvider.AllLowPriceTimes);
            }).WithName("GetLatestPriceReadings");
            priceEndpoints.MapGet("/task", ([FromServices] PriceProvider priceProvider) =>
            {
                return Results.Ok(priceProvider.PriceTask.Status.ToString());
            }).WithName("GetPriceProviderTask");
        }
    }
}
