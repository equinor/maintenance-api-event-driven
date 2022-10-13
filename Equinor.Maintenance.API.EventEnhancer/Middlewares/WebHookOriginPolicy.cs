using Equinor.Maintenance.API.EventEnhancer.Constants;
using Microsoft.AspNetCore.Authorization;

namespace Equinor.Maintenance.API.EventEnhancer.Middlewares;
public class WebHookOriginRequirement : IAuthorizationRequirement
{
    public string[] AllowedWebHookOrigins { get; }

    public WebHookOriginRequirement(string[] allowedWebHookOrigins) { AllowedWebHookOrigins = allowedWebHookOrigins; }
}

public class WebHookOriginHandler : AuthorizationHandler<WebHookOriginRequirement>
{
    private readonly HttpContext _httpContext;
    public WebHookOriginHandler(IHttpContextAccessor accessor)
    {
        _httpContext = accessor.HttpContext ?? throw new InvalidOperationException("HttpContext was null");
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext handlerContext, WebHookOriginRequirement requirement)
    {
        if (_httpContext.Request.Headers.TryGetValue(Names.WebHookRequestHeader, out var webHookOrigin)
            && requirement.AllowedWebHookOrigins.Contains(webHookOrigin.ToString()))
        {
            _httpContext.Response.Headers.TryAdd(Names.WebHookAllowHeader, webHookOrigin);
            handlerContext.Succeed(requirement);
        }
        else
            handlerContext.Fail();

        return Task.CompletedTask;
    }
}
