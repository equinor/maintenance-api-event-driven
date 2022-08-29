using System.Net.Mime;
using Equinor.Maintenance.API.EventEnhancer.ConfigSections;
using Equinor.Maintenance.API.EventEnhancer.Constants;
using Equinor.Maintenance.API.EventEnhancer.Logging;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Identity.Web;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddTransient<HttpContextEnricher>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddApplicationInsightsTelemetry(builder.Configuration);
builder.Host.UseSerilog((_, services, lc) =>
                        {
                            lc
                                .Enrich.FromLogContext()
                                .Enrich.With(services.GetRequiredService<HttpContextEnricher>())
                                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} by user {User}{NewLine}{Exception}")
                                .WriteTo.ApplicationInsights(services.GetRequiredService<TelemetryConfiguration>(), TelemetryConverter.Traces, restrictedToMinimumLevel:LogEventLevel.Warning);
                        });

builder.Services.AddOptions<AzureAd>()
       .Bind(builder.Configuration.GetSection(nameof(AzureAd)))
       .Validate(config => config.AllowedWebHookOrigins.Any(), "AzureConfig:AllowedWebHookOrigins must be populated")
       .Validate(config => !string.IsNullOrWhiteSpace(config.ClientId), "AzureConfig:ClientId must be populated")
       .Validate(config => !string.IsNullOrWhiteSpace(config.ClientSecret), "AzureConfig:ClientSecret must be populated")
       .ValidateOnStart();
builder.Services.AddScoped<LogOriginHeader>();

builder.Services
       .AddMicrosoftIdentityWebApiAuthentication(builder.Configuration,
                                                 subscribeToJwtBearerMiddlewareDiagnosticsEvents: builder.Environment.IsDevelopment());

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseMiddleware<LogOriginHeader>();
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

//var pattern = $"{(app.Environment.IsDevelopment() ? string.Empty : "/maintenance-api-event-driven-internal")}/maintenance-events";
const string pattern = "/maintenance-events";

app.MapPost(pattern, () => new OkObjectResult("Hello from Mini Api"))
   .WithName("HelloApi");

app.MapMethods(pattern,
               new[] { HttpMethod.Options.ToString() },
               (IOptions<AzureAd> azureConfig, HttpContext ctx) =>
               {
                   ctx.Response.StatusCode = StatusCodes.Status403Forbidden;

                   if (!ctx.Request.Headers.TryGetValue(Names.WebHookRequestHeader, out var webHookOrigin)
                       || !azureConfig.Value.AllowedWebHookOrigins.Contains(webHookOrigin.ToString())) return Task.CompletedTask;

                   ctx.Response.Headers.Allow       = new StringValues(HttpMethod.Post.ToString());
                   ctx.Response.Headers.ContentType = new StringValues(MediaTypeNames.Application.Json);
                   ctx.Response.StatusCode          = StatusCodes.Status200OK;
                   ctx.Response.Headers.TryAdd(Names.WebHookAllowHeader, webHookOrigin);

                   return Task.CompletedTask;
               })
   .WithName("WebHookOptions")
   .RequireAuthorization();

app.Run();
