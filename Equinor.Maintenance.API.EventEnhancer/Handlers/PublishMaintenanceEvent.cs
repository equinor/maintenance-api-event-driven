using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.Messaging.ServiceBus;
using Equinor.Maintenance.API.EventEnhancer.Constants;
using Equinor.Maintenance.API.EventEnhancer.MaintenanceApiClient.Requests;
using Equinor.Maintenance.API.EventEnhancer.Models;
using JetBrains.Annotations;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Microsoft.Net.Http.Headers;

namespace Equinor.Maintenance.API.EventEnhancer.Handlers;

public record PublishMaintenanceEventResult(MaintenanceEventHook? Data, int StatusCode);

public class PublishMaintenanceEventQuery : IRequest<PublishMaintenanceEventResult>
{
    public MaintenanceEventPublish MaintenanceEventPublish { get; }

    public PublishMaintenanceEventQuery(MaintenanceEventPublish maintenanceEventPublish) { MaintenanceEventPublish = maintenanceEventPublish; }
}

[UsedImplicitly]
public class PublishMaintenanceEvent : IRequestHandler<PublishMaintenanceEventQuery, PublishMaintenanceEventResult>
{
    private readonly ServiceBusClient _serviceBus;
    private readonly ITokenAcquisition _tokenAcquisition;
    private readonly IConfiguration _config;
    private readonly HttpClient _client;
    private readonly ILogger<PublishMaintenanceEvent> _logger;

    public PublishMaintenanceEvent(
        ServiceBusClient serviceBus,

        
        
        ITokenAcquisition tokenAcquisition,
        IConfiguration config,
        IHttpClientFactory factory,
        ILogger<PublishMaintenanceEvent> logger)
    {
        _serviceBus       = serviceBus;
        _tokenAcquisition = tokenAcquisition;
        _config           = config;
        _client           = factory.CreateClient(Names.MainteanceApi);
        _logger           = logger;
    }

    public async Task<PublishMaintenanceEventResult> Handle(PublishMaintenanceEventQuery query, CancellationToken cancellationToken)
    {
        var data           = query.MaintenanceEventPublish.Data;
        var objectId       = data.ObjectId.TrimStart('0');
        var tokenAwaitable = _tokenAcquisition.GetAccessTokenForAppAsync($"{_config["MaintenanceApiClientId"]}/.default");

        var request = data switch
                      {
                          (_, "BUS2007", _) => WorkorderBuilder.BuildCorrectiveLookup(objectId),
                          (_, "BUS2078", _) => MaintenanceRecordsBuilder.BuildFailureReportLookup(objectId),
                          _                 => throw new ArgumentOutOfRangeException(nameof(data))
                      };

        var tokenHeader = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, await tokenAwaitable);
        var requestMessage = new HttpRequestMessage
                             {
                                 RequestUri = new Uri(request, UriKind.Relative),
                                 Headers    = { { HeaderNames.Authorization, tokenHeader.ToString() } }
                             };
        var result = await _client.SendAsync(requestMessage, cancellationToken);
        

        var processedResult = await HandleResult(query, cancellationToken, result, data.Event, objectId);
        if (processedResult.Data is null && processedResult.StatusCode == StatusCodes.Status301MovedPermanently)
        {
            var requestRedirectMessage = new HttpRequestMessage
                                         {
                                             RequestUri = result.Headers.Location,
                                             Headers    = { { HeaderNames.Authorization, tokenHeader.ToString() } }
                                         };
            var redirectResult = await _client.SendAsync(requestRedirectMessage, cancellationToken);

            return await HandleResult(query, cancellationToken, redirectResult, data.Event, objectId);
        }

        return processedResult;
    }

    private async Task<PublishMaintenanceEventResult> HandleResult(
        PublishMaintenanceEventQuery query,
        CancellationToken cancellationToken,
        HttpResponseMessage result,
        string @event,
        string objectId)
    {
        if (!result.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to get data from Maintenance API{Newline}Response Code: {@Code}{NewLine}{ResponseContent}",
                             Environment.NewLine,
                             result.StatusCode,
                             Environment.NewLine,
                             await result.Content.ReadAsStringAsync(cancellationToken));

            return new PublishMaintenanceEventResult(null, (int)result.StatusCode);
        }

        var (type, sourcePart) = CheckEventAndSetProps(@event, "activity-report"); //fix this
        var messageToHook = new MaintenanceEventHook("1.0",
                                                     type,
                                                     query.MaintenanceEventPublish.Id,
                                                     query.MaintenanceEventPublish.Time,
                                                     objectId,
                                                     sourcePart,
                                                     await result.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: cancellationToken));
        var maintenanceEvent = new ServiceBusMessage(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(messageToHook)));
        var sender           = _serviceBus.CreateSender(Names.Topic);
        await sender.SendMessageAsync(maintenanceEvent, cancellationToken);
        await sender.CloseAsync(cancellationToken);

        return new PublishMaintenanceEventResult(messageToHook, (int)result.StatusCode);
    }

    private (string, string) CheckEventAndSetProps(string @event, string input)
    {
        var sourcePart = "https://equinor.github.io/maintenance-api-event-driven-docs/#tag/{0}";
        var type       = "com.equinor.maintenance-events.{0}";
        switch (@event)
        {
            case "CREATED":
                SetMetaData(ref type, ref sourcePart, $"{input}.created");

                break;
            case "RELEASED":
                SetMetaData(ref type, ref sourcePart, $"{input}.released");

                break;
            case "ZCHANGED":
                SetMetaData(ref type, ref sourcePart, $"{input}.changed");

                break;
        }

        return (type, sourcePart);
    }

    private void SetMetaData(ref string typeInput, ref string sourceInput, string input)
    {
        typeInput   = string.Format(typeInput, input);
        sourceInput = string.Format(sourceInput, typeInput);
    }
}
