using System.Net.Mime;
using Equinor.Maintenance.API.EventEnhancer.ConfigSections;
using Equinor.Maintenance.API.EventEnhancer.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Identity.Web;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddTransient<HttpContextEnricher>();
builder.Services.AddHttpContextAccessor();
builder.Host.UseSerilog((_, services, lc) =>
                        {
                            lc
                                .Enrich.FromLogContext()
                                .Enrich.With(services.GetService<HttpContextEnricher>())
                              .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} by user {User}{NewLine}{Exception}");
                        });

builder.Services.AddOptions<AzureAd>()
       .Bind(builder.Configuration.GetSection(nameof(AzureAd)))
       .Validate(config => config.AllowedWebHookOrigins.Any(), "AzureConfig:AllowedWebHookOrigins must be populated")
       .Validate(config => !string.IsNullOrWhiteSpace(config.ClientId), "AzureConfig:ClientId must be populated")
       .Validate(config => !string.IsNullOrWhiteSpace(config.ClientSecret), "AzureConfig:ClientSecret must be populated")
       .ValidateOnStart();


builder.Services
       .AddMicrosoftIdentityWebApiAuthentication(builder.Configuration, 
                                                 subscribeToJwtBearerMiddlewareDiagnosticsEvents:builder.Environment.IsDevelopment());

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => new OkObjectResult("Hello from Mini Api"))
.WithName("HelloApi");

app.MapMethods("/", new []{"OPTIONS"},
               (IOptions<AzureAd> azureConfig, HttpContext ctx) =>
               {
                   ctx.Response.StatusCode = StatusCodes.Status403Forbidden; //forbidden makes more sense than 405

                   if (!ctx.Request.Headers.TryGetValue("WebHook-Request-Origin", out var webHookOrigin)
                       || !azureConfig.Value.AllowedWebHookOrigins.Contains(webHookOrigin.ToString())) return Task.CompletedTask;
                   
                   
                   ctx.Response.Headers.Allow       = new StringValues("GET");
                   ctx.Response.Headers.ContentType = new StringValues(MediaTypeNames.Application.Json);
                   ctx.Response.StatusCode          = StatusCodes.Status200OK;
                   ctx.Response.Headers.TryAdd("WebHook-Allowed-Origin", webHookOrigin);

                   return Task.CompletedTask;
               })
   .WithName("WebHookOptions")
   .RequireAuthorization();

app.Run();
