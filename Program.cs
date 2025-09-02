using HeatHarmony.Config;
using HeatHarmony.Helpers;
using HeatHarmony.Helpers.Impl;
using HeatHarmony.Providers;

var builder = WebApplication.CreateSlimBuilder(args);

GlobalConfig.ApiKey = builder.Configuration["ApiKey"];
GlobalConfig.PricesUrl = builder.Configuration["PricesUrl"];
GlobalConfig.HeishaUrl = builder.Configuration["HeishaUrl"];
GlobalConfig.ShellyTRVConfig = builder.Configuration.GetRequiredSection("ShellyTRV").Get<List<GlobalConfig.ShellyTRV>>();
GlobalConfig.OumanConfig = builder.Configuration.GetRequiredSection("Ouman").Get<GlobalConfig.Ouman>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IRequestProvider, RequestProvider>();
builder.Services.AddSingleton<OumanProvider>();

builder.Services.AddKeyedSingleton<IHeatPoller, HeatPoller>("heatPoller");

builder.Services.AddHttpClient();
var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "HeatHarmony API collection");
    options.RoutePrefix = string.Empty;
});

app.MapGet("/heatPoller", ([FromKeyedServices("heatPoller")] HeatPoller heatPoller) => heatPoller.Updates);
app.MapGet("/heatStatus", ([FromKeyedServices("heatPoller")] HeatPoller heatPoller) => heatPoller.GetStatuses());

app.Run();