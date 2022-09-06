using Equinor.Maintenance.API.EventEnhancer.ConfigSections;
using Equinor.Maintenance.API.EventEnhancer.Constants;
using Microsoft.Extensions.Options;

namespace Equinor.Maintenance.API.EventEnhancer.Middlewares;

public class OriginCheck : IMiddleware
{
    private readonly AzureAd _azureAd;
    public OriginCheck(IOptions<AzureAd> azureAd) { _azureAd = azureAd.Value; }
    public async Task InvokeAsync(HttpContext ctx, RequestDelegate next)
    {

        if (ctx.Request.Headers.TryGetValue(Names.WebHookRequestHeader, out var webHookOrigin)
            && _azureAd.AllowedWebHookOrigins.Contains(webHookOrigin.ToString()))
        {
            ctx.Response.Headers.TryAdd(Names.WebHookAllowHeader, webHookOrigin);

            await next.Invoke(ctx);
        }
        else
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
    }
}
