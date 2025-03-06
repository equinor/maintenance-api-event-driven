using System.Net.Mime;
using Equinor.Maintenance.API.EventEnhancer.Constants;
using Equinor.Maintenance.API.EventEnhancer.Handlers;
using Equinor.Maintenance.API.EventEnhancer.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace Equinor.Maintenance.API.EventEnhancer.Routes;

public static class MaintenanceEvents
{
    private const string Pattern = "/maintenance-events";

    public static void MapMaintenanceEventRoutes(this WebApplication app)
    {
        var group = app.MapGroup(Pattern).RequireAuthorization(Policy.Publish);


        group.MapPost("/", Publish)
            .WithName("MaintenanceEventPublish");


        app.MapMethods(Pattern,
                [HttpMethod.Options.ToString()],
                HandleOptionsRequest)
            .WithName("MaintenanceEventHandshake")
            .RequireAuthorization(Policy.WebHookOrigin);
    }
private static Task HandleOptionsRequest(HttpContext ctx)
                {
                    ctx.Response.Headers.Allow = new StringValues(HttpMethod.Post.ToString());
                    ctx.Response.Headers.ContentType = new StringValues(MediaTypeNames.Application.Json);
                    ctx.Response.StatusCode = StatusCodes.Status200OK;

                    return Task.CompletedTask;
    }
    public static async Task<IResult> Publish([FromBody] MaintenanceEventPublish body, IMediator mediator, CancellationToken cancelToken)
    {
        var result = await mediator.Send(new PublishMaintenanceEventQuery(body), cancelToken);

        return result.StatusCode < 399
            ? Results.Created(string.Empty, result.Data)
            : Results.StatusCode(result.StatusCode);
    }
}