using HeatHarmony.Config;
using HeatHarmony.Extensions;
using HeatHarmony.Providers;
using HeatHarmony.Routes;
using HeatHarmony.Routes.Filters;
using HeatHarmony.Routes.Middlewares;
using HeatHarmony.Workers;
using Microsoft.AspNetCore.Routing.Constraints;
using Microsoft.OpenApi;

var builder = WebApplication.CreateSlimBuilder(args);

GlobalConfig.ApiKey = builder.Configuration["ApiKey"];
GlobalConfig.PricesUrl = builder.Configuration["PricesUrl"];
GlobalConfig.HeishaUrl = builder.Configuration["HeishaUrl"];
GlobalConfig.Shelly3EMUrl = builder.Configuration["Shelly3EMUrl"];
GlobalConfig.ApiDocumentConfig = builder.Configuration.GetRequiredSection("ApiDocument").Get<GlobalConfig.ApiDocument>()!;
GlobalConfig.ShellyTRVConfig = builder.Configuration.GetRequiredSection("ShellyTRV").Get<List<GlobalConfig.ShellyTRV>>();
GlobalConfig.OumanConfig = builder.Configuration.GetRequiredSection("Ouman").Get<GlobalConfig.Ouman>();
GlobalConfig.OilBurnerShellyUrl = builder.Configuration["OilBurnerShellyUrl"];

builder.Services.Configure<RouteOptions>(options => options.SetParameterPolicy<RegexInlineRouteConstraint>("regex"));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "HeatHarmony API",
        Version = "v1",
        Description = "API collection for HeatHarmony services",
    });
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "API Key needed to access the endpoints",
        Name = GlobalConst.ApiKeyHeaderName,
        Type = SecuritySchemeType.ApiKey
    });
    options.AddSecurityRequirement((document) => new OpenApiSecurityRequirement()
    {
        [new OpenApiSecuritySchemeReference("ApiKey", document)] = []
    });
    options.OperationFilter<AppStatusFilter>();
});

builder.Services.AddHostedService<HeatAutomationWorker>();
builder.Services.AddSingleton<IRequestProvider, RequestProvider>();
builder.Services.AddSingleton<HeishaMonProvider>();
builder.Services.AddSingleton<OumanProvider>();
builder.Services.AddSingleton<PriceProvider>();
builder.Services.AddSingleton<TRVProvider>();
builder.Services.AddSingleton<EMProvider>(); 
builder.Services.AddSingleton<OilBurnerProvider>();
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
app.UseMiddleware<ApiKeyMiddleware>();
app.UseMiddleware<ApiVersionHeaderMiddleware>();
app.MapHeishaMonEndpoints();
app.MapOumanEndPoints();
app.MapTRVEndPoints();
app.MapHeatAutomationEndpoints();
app.MapPriceEndpoints();
app.MapEmEndPoints();
app.MapOilBurnerEndPoints();
app.MapAppStatusEndpoints();

app.Run();