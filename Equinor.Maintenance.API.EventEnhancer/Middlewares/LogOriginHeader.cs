using System.Text;
using Equinor.Maintenance.API.EventEnhancer.Constants;
using Microsoft.Net.Http.Headers;

namespace Equinor.Maintenance.API.EventEnhancer.Middlewares;

public class LogOriginHeader(ILogger<LogOriginHeader> logger) : IMiddleware
{
    private readonly IEnumerable<string> _redactedHeaders = [HeaderNames.Authorization];

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var request = context.Request;
        request.EnableBuffering();
        try
        {
            await next.Invoke(context);
        }
        catch (Exception e)
        {
            logger.LogError(e.Message);
        }
        finally
        {
            request.Body.Position = 0;
            var buffer = new byte[Convert.ToInt32(request.ContentLength)];
            _ = await request.Body.ReadAsync(buffer);
            //get body string here...
            var requestContent = Encoding.UTF8.GetString(buffer);
            logger.LogTrace("SAP Payload: {@Payload}", requestContent);
            request.Body.Position = 0;
            if (context.Request.Headers.TryGetValue(Names.WebHookRequestHeader, out var webHookOrigin))
                logger.LogTrace("Header {HeaderName}: {WebHookOrigin}", Names.WebHookRequestHeader, webHookOrigin);
            else
                logger.LogWarning("Could not find header: {HeaderName}", Names.WebHookRequestHeader);

            var iteration = 1;
            foreach (var header in context.Request.Headers)
            {
                var headerValue = _redactedHeaders.Contains(header.Key) ? "[Redacted]" : header.Value.FirstOrDefault();

                logger.LogTrace("Request Header #{Iteration}: {Key}:{Header}", iteration++, header.Key, headerValue);
            }
        }
    }
}
