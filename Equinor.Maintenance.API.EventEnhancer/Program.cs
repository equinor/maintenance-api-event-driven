using System.Net.Mime;
using System.Security.Cryptography.X509Certificates;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Equinor.Maintenance.API.EventEnhancer.ConfigSections;
using Equinor.Maintenance.API.EventEnhancer.Constants;
using Equinor.Maintenance.API.EventEnhancer.Handlers;
using Equinor.Maintenance.API.EventEnhancer.Middlewares;
using Equinor.Maintenance.API.EventEnhancer.Models;
using FluentValidation;
using MediatR;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Primitives;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.JsonWebTokens;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

var builder = WebApplication.CreateBuilder(args);

var services = builder.Services;
var config   = builder.Configuration;
//var environment = builder.Environment;

// Add services to the container.
if (builder.Environment.IsEnvironment("Sandbox")) config.AddUserSecrets<Program>(optional: true);

config.AddAzureKeyVault(new Uri(config.GetConnectionString(nameof(ConnectionStrings.KeyVault)) ??
                                throw new ValidationException("Azure Keyvault Url must be populated")),
    new DefaultAzureCredential(),
    new AzureKeyVaultConfigurationOptions { ReloadInterval = TimeSpan.FromHours(0.5) });

services.AddOptions<AzureAd>()
    .Bind(config.GetSection(Constants.AzureAd))
    .Validate(ad => !string.IsNullOrWhiteSpace(ad.Instance), "Instance must be populated")
    .ValidateOnStart();

services.AddHttpContextAccessor();
services.AddApplicationInsightsTelemetry(opts => opts.ConnectionString
    = config.GetConnectionString(nameof(ConnectionStrings.ApplicationInsights)));

builder.Host.UseSerilog((ctx, svcs, lc) =>
{
    lc.ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console(theme: AnsiConsoleTheme.Literate,
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3} {SourceContext:l}] {Message:lj}{NewLine}{Exception}")
        .WriteTo.ApplicationInsights(svcs.GetRequiredService<TelemetryConfiguration>(),
            TelemetryConverter.Traces,
            LogEventLevel.Error);
});
services.AddScoped<LogOriginHeader>();
services.AddScoped<IAuthorizationHandler, WebHookOriginHandler>();

// services.AddMicrosoftIdentityWebApiAuthentication(config,
//                                                   subscribeToJwtBearerMiddlewareDiagnosticsEvents:
//                                                   config.GetValue<bool>("SubscribeToDiagnosticEvents"));
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(options => config.Bind(Constants.AzureAd, options),
        options =>
        {
            config.Bind(Constants.AzureAd, options);
            // options.ClientSecret = null;
            options.ClientCertificates =
            [
                new CertificateDescription
                {
                    SourceType = CertificateSource.Base64Encoded,
                    Base64EncodedValue = config.GetValue<string>("AuthCertForMaintenanceAPI"),
                    X509KeyStorageFlags = X509KeyStorageFlags.MachineKeySet
                }
            ];
        })
    .EnableTokenAcquisitionToCallDownstreamApi(options => config.Bind(Constants.AzureAd, options))
    .AddInMemoryTokenCaches();

services.AddHttpClient(Names.MainteanceApi,
        cli => cli.BaseAddress = new Uri(config.GetConnectionString(nameof(ConnectionStrings.MaintenanceApi))))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

services.AddSingleton<IConfidentialClientApplication>(_ =>
{
    var azureAdConfig = config.GetRequiredSection(Constants.AzureAd).Get<AzureAd>();

    return ConfidentialClientApplicationBuilder.Create(azureAdConfig!.ClientId)
        .WithCertificate(new X509Certificate2(Convert.FromBase64String(config["AuthCertForMaintenanceAPI"] ??
                                                                       throw new InvalidOperationException("SystemUser Cert null"))))
        .WithTenantId(azureAdConfig.TenantId)
        .Build();
});

services.AddAuthorization(opts =>
{
    opts.AddPolicy(Policy.Publish,
        policyBuilder =>
        {
            policyBuilder.RequireRole(Role.Publish);
            policyBuilder.RequireClaim(JwtRegisteredClaimNames.Azp,
                config.GetSection("AllowedClients")
                    .AsEnumerable()
                    .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
                    .Select(pair => pair.Value)!);
        });
    opts.AddPolicy(Policy.WebHookOrigin,
        policyBuilder =>
        {
            policyBuilder.AddRequirements(new WebHookOriginRequirement(config
                .GetSection("AzureAd:AllowedWebHookOrigins")
                .AsEnumerable()
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
                .Select(pair => pair.Value!)
                .ToArray()));
        });
});

services.AddAzureClients(clientBuilder =>
{
    clientBuilder.AddServiceBusClient(config.GetConnectionString(nameof(ConnectionStrings.ServiceBus)));
});
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

            return result.StatusCode < 399
                ? Results.Created(string.Empty, result.Data)
                : Results.StatusCode(result.StatusCode);
        })
    .WithName("MaintenanceEventPublish")
    .RequireAuthorization(Policy.Publish);

app.MapMethods(pattern,
        new[] { HttpMethod.Options.ToString() },
        ctx =>
        {
            ctx.Response.Headers.Allow = new StringValues(HttpMethod.Post.ToString());
            ctx.Response.Headers.ContentType = new StringValues(MediaTypeNames.Application.Json);
            ctx.Response.StatusCode = StatusCodes.Status200OK;

            return Task.CompletedTask;
        })
    .WithName("MaintenanceEventHandshake")
    .RequireAuthorization(Policy.Publish, Policy.WebHookOrigin);

app.Run();