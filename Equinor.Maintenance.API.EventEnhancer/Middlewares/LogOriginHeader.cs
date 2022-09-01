using Equinor.Maintenance.API.EventEnhancer.Constants;

namespace Equinor.Maintenance.API.EventEnhancer.Middlewares;

public class LogOriginHeader : IMiddleware
{
    private readonly ILogger<LogOriginHeader> _logger;

    public LogOriginHeader(ILogger<LogOriginHeader> logger) { _logger = logger; }
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        await next.Invoke(context);
        
        if (context.Request.Headers.TryGetValue(Names.WebHookRequestHeader, out var webHookOrigin)) 
            _logger.LogTrace("Header {HeaderName}: {WebHookOrigin}", Names.WebHookRequestHeader,webHookOrigin);
        else
            _logger.LogWarning("Could not find header: {HeaderName}", Names.WebHookRequestHeader);
        
    }
}
