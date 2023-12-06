using HeatHarmony.Helpers;
using HeatHarmony.Helpers.Impl;

var builder = WebApplication.CreateSlimBuilder(args);

var heatPoller = new HeatPoller(builder.Configuration);
builder.Services.AddSingleton<IHeatPoller>(heatPoller);

var app = builder.Build();

app.MapGet("/heatPoller", () => heatPoller.Updates);
app.MapGet("/heatStatus", () => heatPoller.GetStatuses());

app.Run();