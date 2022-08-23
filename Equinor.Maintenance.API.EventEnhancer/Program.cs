using System.Net.Mime;
using Equinor.Maintenance.API.EventEnhancer.ConfigSections;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Host.UseSerilog((ctx, lc) => lc
                                     .WriteTo.Console());

builder.Services.AddOptions<AzureConfig>()
       .Bind(builder.Configuration.GetSection(nameof(AzureConfig)))
       .Validate(config => config.AllowedWebHookOrigins.Any(), "AzureConfig:AllowedWebHookOrigins must be populated")
       .Validate(config => !string.IsNullOrWhiteSpace(config.ClientId), "AzureConfig:ClientId must be populated")
       .Validate(config => !string.IsNullOrWhiteSpace(config.ClientSecret), "AzureConfig:ClientSecret must be populated")
       .ValidateOnStart();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

app.MapGet("/", () => new OkObjectResult("Hello from Mini Api"))
.WithName("HelloApi");

app.MapMethods("/", new []{"OPTIONS"},
               (IOptions<AzureConfig> azureConfig, HttpContext ctx) =>
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
   .WithName("WebHookOptions");

app.Run();
