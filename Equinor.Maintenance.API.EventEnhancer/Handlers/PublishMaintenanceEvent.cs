using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.Messaging.ServiceBus;
using Equinor.Maintenance.API.EventEnhancer.Constants;
using Equinor.Maintenance.API.EventEnhancer.MaintenanceRequests;
using Equinor.Maintenance.API.EventEnhancer.Models;
using JetBrains.Annotations;
using MediatR;
using Microsoft.Identity.Web;

namespace Equinor.Maintenance.API.EventEnhancer.Handlers;

public class PublishMaintenanceEventQuery : IRequest<MaintenanceEventHook>
{
    public MaintenanceEventPublish MaintenanceEventPublish { get; }

    public PublishMaintenanceEventQuery(MaintenanceEventPublish maintenanceEventPublish) { MaintenanceEventPublish = maintenanceEventPublish; }
}

[UsedImplicitly]
public class PublishMaintenanceEvent : IRequestHandler<PublishMaintenanceEventQuery, MaintenanceEventHook>
{
    private readonly ServiceBusClient _serviceBus;
    private readonly IDownstreamWebApi _dsApi;

    public PublishMaintenanceEvent(ServiceBusClient serviceBus, IDownstreamWebApi dsApi)
    {
        _serviceBus = serviceBus;
        _dsApi      = dsApi;
    }

    public async Task<MaintenanceEventHook> Handle(PublishMaintenanceEventQuery query, CancellationToken cancellationToken)
    {
        var    builder  = new WorkorderRequestBuilder();
        var    data     = query.MaintenanceEventPublish.Data;
        var    objectId = data.ObjectId.TrimStart('0');
        string request;
        var    sourcePart = string.Empty;
        var    type       = string.Empty;
        switch (data)
        {
            case (_, "BUS2007", var eventType):
                request = builder.BuildCorrectiveLookup(objectId);
                switch (eventType)
                {
                    case "CREATED":
                        sourcePart = "CorrectiveWorkOrder";
                        type       = "com.equinor.maintenance-events.corrective-work-orders.created";

                        break;
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(data));
        }

        var result = await _dsApi.CallWebApiForAppAsync(Names.MainteanceApi,
                                                        opts =>
                                                        {
                                                            opts.RelativePath = request;
                                                        });
        var messageToHook = new MaintenanceEventHook("1.0",
                                                     type,
                                                     "A1234-2134",
                                                     "2022-09-01T12:32:00Z",
                                                     objectId,
                                                     $"https://equinor.github.io/maintenance-api-event-driven-docs/#tag/{sourcePart}",
                                                     await result.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: cancellationToken));
        var maintenanceEvent = new ServiceBusMessage(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(messageToHook)));
        var sender           = _serviceBus.CreateSender(Names.Topic);
        await sender.SendMessageAsync(maintenanceEvent, cancellationToken);
        await sender.CloseAsync(cancellationToken);

        return messageToHook;
    }
}
