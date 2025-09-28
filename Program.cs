using HeatHarmony.Config;
using HeatHarmony.Providers;
using HeatHarmony.Routes;
using HeatHarmony.Routes.Middlewares;
using HeatHarmony.Workers;
using Microsoft.AspNetCore.Routing.Constraints;
using HeatHarmony.Extensions;

var builder = WebApplication.CreateSlimBuilder(args);

GlobalConfig.ApiKey = builder.Configuration["ApiKey"];
GlobalConfig.PricesUrl = builder.Configuration["PricesUrl"];
GlobalConfig.HeishaUrl = builder.Configuration["HeishaUrl"];
GlobalConfig.ApiDocumentConfig = builder.Configuration.GetRequiredSection("ApiDocument").Get<GlobalConfig.ApiDocument>()!;
GlobalConfig.ShellyTRVConfig = builder.Configuration.GetRequiredSection("ShellyTRV").Get<List<GlobalConfig.ShellyTRV>>();
GlobalConfig.OumanConfig = builder.Configuration.GetRequiredSection("Ouman").Get<GlobalConfig.Ouman>();

builder.Services.Configure<RouteOptions>(options => options.SetParameterPolicy<RegexInlineRouteConstraint>("regex"));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHostedService<HeatAutomationWorker>();
builder.Services.AddSingleton<IRequestProvider, RequestProvider>();
builder.Services.AddSingleton<HeishaMonProvider>();
builder.Services.AddSingleton<OumanProvider>();
builder.Services.AddSingleton<PriceProvider>();
builder.Services.AddSingleton<TRVProvider>();
builder.Services.AddSingleton<HeatAutomationWorkerProvider>();

builder.AddHttpClients();
var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "HeatHarmony API collection");
    options.RoutePrefix = string.Empty;
});

app.UseRouting();
//app.UseMiddleware<ApiKeyMiddleware>();
app.UseMiddleware<ApiVersionHeaderMiddleware>();
app.MapHeishaMonEndpoints();
app.MapOumanEndPoints();
app.MapTRVEndPoints();
app.MapHeatAutomationEndpoints();

app.Run();