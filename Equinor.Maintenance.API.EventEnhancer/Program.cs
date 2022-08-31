using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Azure.Messaging.ServiceBus;
using Equinor.Maintenance.API.EventEnhancer.ConfigSections;
using Equinor.Maintenance.API.EventEnhancer.Constants;
using Equinor.Maintenance.API.EventEnhancer.Logging;
using Equinor.Maintenance.API.EventEnhancer.Models;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Logging;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddTransient<HttpContextEnricher>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddApplicationInsightsTelemetry(opts => opts.ConnectionString
                                                     = builder.Configuration.GetConnectionString(nameof(ConnectionStrings.ApplicationInsights)));
builder.Host.UseSerilog((_, services, lc) =>
                        {
                            lc
                                .Enrich.FromLogContext()
                                .Enrich.With(services.GetRequiredService<HttpContextEnricher>())
                                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} by user {User}{NewLine}{Exception}")
                                .WriteTo.ApplicationInsights(services.GetRequiredService<TelemetryConfiguration>(),
                                                             TelemetryConverter.Traces,
                                                             restrictedToMinimumLevel: LogEventLevel.Warning);
                        });

builder.Services.AddOptions<AzureAd>()
       .Bind(builder.Configuration.GetSection(nameof(AzureAd)))
       .Validate(config => config.AllowedWebHookOrigins.Any(), "AllowedWebHookOrigins must be populated")
       .Validate(config => !string.IsNullOrWhiteSpace(config.ClientId), "ClientId must be populated")
       .Validate(config => !string.IsNullOrWhiteSpace(config.ClientSecret), "ClientSecret must be populated")
       .ValidateOnStart();

builder.Services.AddScoped<LogOriginHeader>();

builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration,
                                                          subscribeToJwtBearerMiddlewareDiagnosticsEvents: builder.Environment.IsDevelopment());

builder.Services.AddAzureClients(clientBuilder =>
                                     clientBuilder.AddServiceBusClient(builder.Configuration
                                                                              .GetConnectionString(nameof(ConnectionStrings.ServiceBus))));

//builder.Services.Configure<JsonOptions>(opts => opts.JsonSerializerOptions.);

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseMiddleware<LogOriginHeader>();
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.Use(async (ctx, next) =>
        {
            var azureConfig = ctx.RequestServices.GetRequiredService<IOptions<AzureAd>>();

            if (ctx.Request.Headers.TryGetValue(Names.WebHookRequestHeader, out var webHookOrigin)
                && azureConfig.Value.AllowedWebHookOrigins.Contains(webHookOrigin.ToString()))
            {
                ctx.Response.Headers.TryAdd(Names.WebHookAllowHeader, webHookOrigin);

                await next.Invoke(ctx);
            }
            else
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        });

const string pattern = "/maintenance-events";

app.MapPost(pattern,
            async (ServiceBusClient serviceBus, [FromBody] MaintenanceEventPublish body) =>
            {
                var jsonobj    = new JsonObject { { "WorkOrderId", body.Id } };
                var messageToHook = new MaintenanceEventHook("1.0",
                                                             "com.equinor.maintenance-events.sas-change-work-orders.created",
                                                             "A1234-2134",
                                                             "2022-09-01T12:32:00Z",
                                                             "123456",
                                                             "https://equinor.github.io/maintenance-api-event-driven-docs/#tag/SAS-Change-Work-orders",
                                                             MediaTypeNames.Application.Json,
                                                             jsonobj);
                var maintenanceEvent = new ServiceBusMessage(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(messageToHook)));
                var sender           = serviceBus.CreateSender(Names.Topic);
                await sender.SendMessageAsync(maintenanceEvent);
                await sender.CloseAsync();

                return new CreatedResult(string.Empty, messageToHook);
            })
   .WithName("MaintenanceEventPublish")
   .RequireAuthorization();

app.MapMethods(pattern,
               new[] { HttpMethod.Options.ToString() },
               ctx =>
               {
                   //ctx.Response.StatusCode = StatusCodes.Status403Forbidden;

                   //if (!ctx.Request.Headers.TryGetValue(Names.WebHookRequestHeader, out var webHookOrigin)
                   //    || !azureConfig.Value.AllowedWebHookOrigins.Contains(webHookOrigin.ToString())) return Task.CompletedTask;

                   ctx.Response.Headers.Allow       = new StringValues(HttpMethod.Post.ToString());
                   ctx.Response.Headers.ContentType = new StringValues(MediaTypeNames.Application.Json);
                   ctx.Response.StatusCode          = StatusCodes.Status200OK;
                   //ctx.Response.Headers.TryAdd(Names.WebHookAllowHeader, webHookOrigin);

                   return Task.CompletedTask;
               })
   .WithName("MaintenanceEventHandshake")
   .RequireAuthorization();

app.Run();
