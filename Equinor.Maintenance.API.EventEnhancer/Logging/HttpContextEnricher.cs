using Serilog.Core;
using Serilog.Events;

namespace Equinor.Maintenance.API.EventEnhancer.Logging;

public class HttpContextEnricher : ILogEventEnricher
{
    private readonly IHttpContextAccessor _accessor;

    public HttpContextEnricher(IHttpContextAccessor accessor) { _accessor = accessor; }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (_accessor.HttpContext == null)
        {
            return;
        }

        var user = _accessor.HttpContext.User.FindFirst("name") ?? _accessor.HttpContext.User.FindFirst("appid");
        if (user is { }) logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("User", user.Value));
    }
}
