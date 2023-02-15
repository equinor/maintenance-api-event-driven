using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.Messaging.ServiceBus;
using Equinor.Maintenance.API.EventEnhancer.Constants;
using Equinor.Maintenance.API.EventEnhancer.ExtensionMethods;
using Equinor.Maintenance.API.EventEnhancer.MaintenanceApiClient.Requests;
using Equinor.Maintenance.API.EventEnhancer.Models;
using JetBrains.Annotations;
using MediatR;
using Microsoft.Identity.Client;
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
    private readonly IConfiguration _config;
    private readonly IConfidentialClientApplication _confidentialClientApplication;
    private readonly HttpClient _client;
    private readonly ILogger<PublishMaintenanceEvent> _logger;
    private readonly ITokenAcquisition _getToken;

    public PublishMaintenanceEvent(
        ServiceBusClient serviceBus,
        IConfiguration config,
        IHttpClientFactory factory,
        IConfidentialClientApplication confidentialClientApplication,
        ILogger<PublishMaintenanceEvent> logger,
        ITokenAcquisition getToken)
    {
        _serviceBus                    = serviceBus;
        _config                        = config;
        _confidentialClientApplication = confidentialClientApplication;
        _client                        = factory.CreateClient(Names.MainteanceApi);
        _logger                        = logger;
        _getToken                 = getToken;
    }

    public async Task<PublishMaintenanceEventResult> Handle(PublishMaintenanceEventQuery query, CancellationToken cancellationToken)
    {
        var data           = query.MaintenanceEventPublish.Data;
        var objectId       = data.ObjectId.TrimStart('0');
        var tokenAwaitable2 = _confidentialClientApplication.AcquireTokenForClient(new[] { $"{_config["MaintenanceApiClientId"]}/.default" }).ExecuteAsync(cancellationToken);
        var tokenAwaitable = _getToken.GetAccessTokenForAppAsync($"{_config["MaintenanceApiClientId"]}/.default" ,tokenAcquisitionOptions:new TokenAcquisitionOptions{CancellationToken = cancellationToken});

        var request = data switch
                      {
                          (_, "BUS2007", _) => WorkorderBuilder.BuildCorrectiveLookup(objectId),
                          (_, "BUS2078", _) => MaintenanceRecordsBuilder.BuildFailureReportLookup(objectId),
                          _                 => throw new ArgumentOutOfRangeException(nameof(data))
                      };
        var tokenHeader2 = new AuthenticationHeaderValue(Microsoft.Identity.Web.Constants.Bearer,  (await tokenAwaitable2).AccessToken);
        var tokenHeader = new AuthenticationHeaderValue(Microsoft.Identity.Web.Constants.Bearer,  await tokenAwaitable);
       
        var requestMessage = new HttpRequestMessage
                             {
                                 RequestUri = new Uri(request, UriKind.Relative),
                                 Headers    = { { HeaderNames.Authorization, tokenHeader.ToString() } }
                             };
        _logger.LogDebug("Calling Maintenance API on {Verb} {Path}", requestMessage.Method.Method, requestMessage.RequestUri.ToString());
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

            
        var (type, sourcePart) = CheckEventAndSetProps(@event, result.RequestMessage?.RequestUri?.Segments[3].TrimEnd('/') ?? "");
        var data = await result.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: cancellationToken);
        var messageToHook = new MaintenanceEventHook("1.0",
                                                     type,
                                                     query.MaintenanceEventPublish.Id,
                                                     query.MaintenanceEventPublish.Time,
                                                     objectId,
                                                     sourcePart,
                                                     data);
        var maintenanceEvent = new ServiceBusMessage(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(messageToHook)));
        var properties = new Dictionary<string, object>()
        {
            { "filter-property-planning-plant-id",  data.GetJsonObjectPropertyValue("planningPlantId").ToString() },
            { "filter-property-active-status-ids", data.GetJsonObjectPropertyValue("activeStatusIds").ToString() },
            { "filter-property-work-center-id", data.GetJsonObjectPropertyValue("workCenterId").ToString() },
            { "filter-property-planner-group-id", data.GetJsonObjectPropertyValue("plannerGroupId").ToString() }
        };
        var statuses = data.GetJsonObjectPropertyValueArray("statuses");
        foreach (var status in statuses)
        {
            var sa = status.AsObject();
            var id = sa.GetJsonObjectPropertyValue("statusId").ToString();
            var isActive = sa.GetJsonObjectPropertyValue("isActive").AsValue();
            properties.Add($"filter-property-status-{id}-is-active", isActive.ToString());
        }
        
        foreach (var property in properties)
        {
            maintenanceEvent.ApplicationProperties.Add(property);
        }
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
