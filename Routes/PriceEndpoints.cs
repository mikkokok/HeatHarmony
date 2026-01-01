using HeatHarmony.DTO;
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

                var response = new PriceTodayResponse
                {
                    Prices = data
                };

                return Results.Ok(response);
            })
            .WithName("GetTodayPrices")
            .Produces<PriceTodayResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status503ServiceUnavailable);

            prices.MapGet("/tomorrow", ([FromServices] PriceProvider priceProvider) =>
            {
                var data = priceProvider.TomorrowPrices;
                if (data.Count == 0)
                    return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

                var response = new PriceTomorrowResponse
                {
                    Prices = data
                };

                return Results.Ok(response);
            })
            .WithName("GetTomorrowPrices")
            .Produces<PriceTomorrowResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status503ServiceUnavailable);

            prices.MapGet("/lowperiods/today", ([FromServices] PriceProvider priceProvider) =>
            {
                var periods = priceProvider.TodayLowPriceTimes;

                var response = new TodayLowPeriodsResponse
                {
                    Periods = periods
                };

                return Results.Ok(response);
            })
            .WithName("GetTodayLowPeriods")
            .Produces<TodayLowPeriodsResponse>(StatusCodes.Status200OK);

            prices.MapGet("/lowperiods/all", ([FromServices] PriceProvider priceProvider) =>
            {
                var response = new AllLowPeriodsResponse
                {
                    Periods = priceProvider.AllLowPriceTimes
                };

                return Results.Ok(response);
            })
            .WithName("GetAllLowPeriods")
            .Produces<AllLowPeriodsResponse>(StatusCodes.Status200OK);

            prices.MapGet("/nightperiod", ([FromServices] PriceProvider priceProvider) =>
            {
                var response = new NightPeriodResponse
                {
                    Period = priceProvider.NightPeriodTimes
                };

                return Results.Ok(response);
            })
            .WithName("GetNightPeriod")
            .Produces<NightPeriodResponse>(StatusCodes.Status200OK);
        }
    }
}
