using System.Net.Mime;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Equinor.Maintenance.API.EventEnhancer.ConfigSections;
using Equinor.Maintenance.API.EventEnhancer.Constants;
using Equinor.Maintenance.API.EventEnhancer.Handlers;
using Equinor.Maintenance.API.EventEnhancer.Middlewares;
using Equinor.Maintenance.API.EventEnhancer.Models;
using MediatR;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Primitives;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.JsonWebTokens;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

var builder = WebApplication.CreateBuilder(args);

var services = builder.Services;
var config   = builder.Configuration;
//var environment = builder.Environment;

// Add services to the container.
services.AddOptions<AzureAd>()
        .Bind(config.GetSection(nameof(AzureAd)))
        //.Validate(ad => ad.AllowedWebHookOrigins.Any(), "AllowedWebHookOrigins must be populated")
        .Validate(ad => !string.IsNullOrWhiteSpace(ad.ClientId), "ClientId must be populated")
        .Validate(ad => !string.IsNullOrWhiteSpace(ad.ClientSecret), "ClientSecret must be populated")
        .Validate(ad => !string.IsNullOrWhiteSpace(ad.TenantId), "TenantId must be populated")
        .Validate(ad => !string.IsNullOrWhiteSpace(ad.Instance), "Instance must be populated")
        .ValidateOnStart();

var azureAd = config.GetSection(nameof(AzureAd)).Get<AzureAd>();
Environment.SetEnvironmentVariable("AZURE_TENANT_ID", azureAd.TenantId);
Environment.SetEnvironmentVariable("AZURE_CLIENT_ID", azureAd.ClientId);
Environment.SetEnvironmentVariable("AZURE_CLIENT_SECRET", azureAd.ClientSecret);
var kvUrl = config.GetConnectionString(nameof(ConnectionStrings.KeyVault));
config["AzureAd:ClientCertificates:0:KeyVaultUrl"] = kvUrl;

config.AddAzureKeyVault(new Uri(kvUrl),
                        new ClientSecretCredential(azureAd.TenantId, azureAd.ClientId, azureAd.ClientSecret),
                        new AzureKeyVaultConfigurationOptions { ReloadInterval = TimeSpan.FromHours(0.5) });

services.AddHttpContextAccessor();
services.AddApplicationInsightsTelemetry(opts => opts.ConnectionString
                                             = config.GetConnectionString(nameof(ConnectionStrings.ApplicationInsights)));
var logSwitch = new LoggingLevelSwitch();
builder.Host.UseSerilog((_, svcs, lc) =>
                        {
                            lc.MinimumLevel.ControlledBy(logSwitch)
                              .MinimumLevel.Override("Microsoft.IdentityModel.LoggingExtensions.IdentityLoggerAdapter", LogEventLevel.Error)
                              .MinimumLevel.Override("Microsoft.Identity.Web.TokenAcquisition", LogEventLevel.Warning)
                              .MinimumLevel.Override("Azure.Core", LogEventLevel.Warning)
                              .MinimumLevel.Override("Azure.Identity", LogEventLevel.Warning)
                              .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                              .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                              .Enrich.FromLogContext()
                              .WriteTo.Console(theme: AnsiConsoleTheme.Literate, outputTemplate:"[{Timestamp:HH:mm:ss} {Level:u3} {SourceContext:l}] {Message:lj}{NewLine}{Exception}")
                              .WriteTo.ApplicationInsights(svcs.GetRequiredService<TelemetryConfiguration>(),
                                                           TelemetryConverter.Traces,
                                                           levelSwitch: logSwitch);
                        });
services.AddSingleton(logSwitch);
services.AddScoped<LogOriginHeader>();
services.AddScoped<IAuthorizationHandler, WebHookOriginHandler>();

services.AddMicrosoftIdentityWebApiAuthentication(config,
                                                  subscribeToJwtBearerMiddlewareDiagnosticsEvents:
                                                  config.GetValue<bool>("SubscribeToDiagnosticEvents"))
        .EnableTokenAcquisitionToCallDownstreamApi()
        .AddInMemoryTokenCaches();

services.AddHttpClient(Names.MainteanceApi,
                       cli => cli.BaseAddress = new Uri(config.GetConnectionString(nameof(ConnectionStrings.MaintenanceApi))))
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

services.AddAuthorization(opts =>
                          {
                              opts.AddPolicy(Policy.Publish,
                                             policyBuilder =>
                                             {
                                                 policyBuilder.RequireRole(Role.Publish);
                                                 policyBuilder.RequireClaim(JwtRegisteredClaimNames.Azp,
                                                                            config.GetSection("AllowedClients")
                                                                                  .AsEnumerable()
                                                                                  .Select(pair => pair.Value));
                                             });
                              opts.AddPolicy(Policy.WebHookOrigin,
                                             policyBuilder =>
                                             {
                                                 policyBuilder.AddRequirements(new WebHookOriginRequirement(azureAd.AllowedWebHookOrigins));
                                             });
                          });

services.AddAzureClients(clientBuilder => clientBuilder.AddServiceBusClient(config.GetConnectionString(nameof(ConnectionStrings.ServiceBus))));
services.AddMediatR(typeof(Program));

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSerilogRequestLogging(opts =>
                             {
                                 opts.EnrichDiagnosticContext = (context, httpContext) =>
                                                                {
                                                                    var id = httpContext.User.Identity?.Name ??
                                                                             httpContext.User.FindFirst(JwtRegisteredClaimNames.Azp)?.Value;
                                                                    if (id is { }) context.Set("Identity", $" by caller {id}");
                                                                };
                                 opts.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms {Identity}";
                             });
app.UseMiddleware<LogOriginHeader>();
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

const string pattern = "/maintenance-events";

app.MapPost(pattern,
            async ([FromBody] MaintenanceEventPublish body, IMediator mediator, CancellationToken cancelToken) =>
            {
                var result = await mediator.Send(new PublishMaintenanceEventQuery(body), cancelToken);

                return result.StatusCode < 399 ? Results.Created(string.Empty, result.Data) : Results.StatusCode(result.StatusCode);
            })
   .WithName("MaintenanceEventPublish")
   .RequireAuthorization(Policy.Publish);

app.MapMethods(pattern,
               new[] { HttpMethod.Options.ToString() },
               ctx =>
               {
                   ctx.Response.Headers.Allow       = new StringValues(HttpMethod.Post.ToString());
                   ctx.Response.Headers.ContentType = new StringValues(MediaTypeNames.Application.Json);
                   ctx.Response.StatusCode          = StatusCodes.Status200OK;

                   return Task.CompletedTask;
               })
   .WithName("MaintenanceEventHandshake")
   .RequireAuthorization(Policy.Publish, Policy.WebHookOrigin);

app.Run();
