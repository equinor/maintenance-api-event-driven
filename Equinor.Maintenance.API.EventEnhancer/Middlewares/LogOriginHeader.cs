using System.Text;
using Equinor.Maintenance.API.EventEnhancer.Constants;

namespace Equinor.Maintenance.API.EventEnhancer.Middlewares;

public class LogOriginHeader : IMiddleware
{
    private readonly ILogger<LogOriginHeader> _logger;

    public LogOriginHeader(ILogger<LogOriginHeader> logger) { _logger = logger; }
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var request = context.Request;
        request.EnableBuffering();
        try
        {
            await next.Invoke(context);
        }
        finally
        {
            request.Body.Position = 0;
            var buffer = new byte[Convert.ToInt32(request.ContentLength)];
            var unused = await request.Body.ReadAsync(buffer, 0, buffer.Length);
            //get body string here...
            var requestContent = Encoding.UTF8.GetString(buffer);
            _logger.LogTrace("SAP Payload: {@Payload}", requestContent);
            request.Body.Position = 0;
            if (context.Request.Headers.TryGetValue(Names.WebHookRequestHeader, out var webHookOrigin)) 
                _logger.LogTrace("Header {HeaderName}: {WebHookOrigin}", Names.WebHookRequestHeader,webHookOrigin);
            else
                _logger.LogWarning("Could not find header: {HeaderName}", Names.WebHookRequestHeader);
        }
    }
}
