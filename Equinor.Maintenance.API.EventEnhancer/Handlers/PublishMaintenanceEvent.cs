using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.Messaging.ServiceBus;
using Equinor.Maintenance.API.EventEnhancer.Constants;
using Equinor.Maintenance.API.EventEnhancer.MaintenanceApiClient.Requests;
using Equinor.Maintenance.API.EventEnhancer.Models;
using JetBrains.Annotations;
using MediatR;
using Microsoft.Identity.Web;

namespace Equinor.Maintenance.API.EventEnhancer.Handlers;

public record PublishMaintenanceEventResult(MaintenanceEventHook? Data, bool Success);

public class PublishMaintenanceEventQuery : IRequest<PublishMaintenanceEventResult>
{
    public MaintenanceEventPublish MaintenanceEventPublish { get; }

    public PublishMaintenanceEventQuery(MaintenanceEventPublish maintenanceEventPublish) { MaintenanceEventPublish = maintenanceEventPublish; }
}

[UsedImplicitly]
public class PublishMaintenanceEvent : IRequestHandler<PublishMaintenanceEventQuery, PublishMaintenanceEventResult>
{
    private readonly ServiceBusClient _serviceBus;
    private readonly IDownstreamWebApi _dsApi;
    private readonly ILogger<PublishMaintenanceEvent> _logger;

    public PublishMaintenanceEvent(ServiceBusClient serviceBus, IDownstreamWebApi dsApi, ILogger<PublishMaintenanceEvent> logger)
    {
        _serviceBus = serviceBus;
        _dsApi      = dsApi;
        _logger     = logger;
    }

    public async Task<PublishMaintenanceEventResult> Handle(PublishMaintenanceEventQuery query, CancellationToken cancellationToken)
    {
        var    data     = query.MaintenanceEventPublish.Data;
        var    objectId = data.ObjectId.TrimStart('0');
        string request;
        var    sourcePart = "https://equinor.github.io/maintenance-api-event-driven-docs/#tag/{0}";
        var    type       = "com.equinor.maintenance-events.{0}";

        switch (data)
        {
            case (_, "BUS2007", var @event):
                request = WorkorderBuilder.BuildCorrectiveLookup(objectId);
                CheckEventAndSetProps(@event, "corrective-work-order");

                break;
            case (_, "BUS2078", var @event):
                request = MaintenanceRecordsBuilder.BuildFailureReportLookup(objectId);
                CheckEventAndSetProps(@event, "activity-report");

                break;
            //note add more cases for mor bus events as mappings become known.
            default:
                throw new ArgumentOutOfRangeException(nameof(data));
        }

        void CheckEventAndSetProps(string @event, string input)
        {
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
        }

        var result = await _dsApi.CallWebApiForAppAsync(Names.MainteanceApi, opts => opts.RelativePath = request);
        if (!result.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to get data from Maintenance API{Newline}Response Code: {@Code}{NewLine}{ResponseContent}",
                             Environment.NewLine,
                             result.StatusCode,
                             Environment.NewLine,
                             await result.Content.ReadAsStringAsync(cancellationToken));

            return new PublishMaintenanceEventResult(null, false);
        }

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

        return new PublishMaintenanceEventResult(messageToHook, true);
    }

    private void SetMetaData(ref string typeInput, ref string sourceInput, string input)
    {
        typeInput   = string.Format(typeInput, input);
        sourceInput = string.Format(sourceInput, typeInput);
    }
}
