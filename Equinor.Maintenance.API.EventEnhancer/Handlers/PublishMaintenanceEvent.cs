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
        var    data     = query.MaintenanceEventPublish.Data;
        var    objectId = data.ObjectId.TrimStart('0');
        string request;
        var    sourcePart = "https://equinor.github.io/maintenance-api-event-driven-docs/#tag/{0}";
        var    type       = "com.equinor.maintenance-events.{0}";
        
        switch (data)
        {
            case (_, "BUS2007", var @event):
                request = WorkorderRequestBuilder.BuildCorrectiveLookup(objectId); 
                CheckEventAndSetProps(@event, "corrective-work-order");

                break;
            case (_, "BUS2078", var @event):
                request = MaintenanceRecordsRequestBuilder.BuildActivityReportLookup(objectId);
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

        return messageToHook;
    }

    private void SetMetaData(ref string typeInput, ref string sourceInput, string input)
    {
        typeInput   = string.Format(typeInput, input);
        sourceInput = string.Format(sourceInput, typeInput);
    }
}
