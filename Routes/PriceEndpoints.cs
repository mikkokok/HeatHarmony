using HeatHarmony.Providers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HeatHarmony.Routes
{
    public static partial class ApiMapper
    {
        public static void MapPriceEndpoints(this WebApplication app)
        {
            var prices = app.MapGroup("/prices")
                            .WithTags("PriceEndpoints");

            prices.MapGet("/today", ([FromServices] PriceProvider priceProvider) =>
            {
                var data = priceProvider.TodayPrices;
                if (data.Count == 0)
                    return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
                return Results.Ok(data);
            })
            .WithName("GetTodayPrices")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status503ServiceUnavailable);

            prices.MapGet("/tomorrow", ([FromServices] PriceProvider priceProvider) =>
            {
                var data = priceProvider.TomorrowPrices;
                if (data.Count == 0)
                    return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
                return Results.Ok(data);
            })
            .WithName("GetTomorrowPrices")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status503ServiceUnavailable);

            prices.MapGet("/lowperiods/today", ([FromServices] PriceProvider priceProvider) =>
            {
                var periods = priceProvider.TodayLowPriceTimes;
                return Results.Ok(periods);
            })
            .WithName("GetTodayLowPeriods")
            .Produces(StatusCodes.Status200OK);

            prices.MapGet("/lowperiods/all", ([FromServices] PriceProvider priceProvider) =>
            {
                return Results.Ok(priceProvider.AllLowPriceTimes);
            })
            .WithName("GetAllLowPeriods")
            .Produces(StatusCodes.Status200OK);

            prices.MapGet("/nightperiod", ([FromServices] PriceProvider priceProvider) =>
            {
                return Results.Ok(priceProvider.NightPeriodTimes);
            })
            .WithName("GetNightPeriod")
            .Produces(StatusCodes.Status200OK);
        }
    }
}
