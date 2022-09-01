using System.Net.Mime;
using System.Text;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Equinor.Maintenance.API.EventEnhancer.Constants;
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
        var result = await _dsApi.CallWebApiForAppAsync(Names.MainteanceApi,
                                                        opts => { opts.RelativePath = "plants/1100/locations?api-version=v1"; });
        var messageToHook = new MaintenanceEventHook("1.0",
                                                     "com.equinor.maintenance-events.sas-change-work-orders.created",
                                                     "A1234-2134",
                                                     "2022-09-01T12:32:00Z",
                                                     "123456",
                                                     "https://equinor.github.io/maintenance-api-event-driven-docs/#tag/SAS-Change-Work-orders",
                                                     MediaTypeNames.Application.Json,
                                                     await result.Content.ReadAsStringAsync(cancellationToken));
        var maintenanceEvent = new ServiceBusMessage(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(messageToHook)));
        var sender           = _serviceBus.CreateSender(Names.Topic);
        await sender.SendMessageAsync(maintenanceEvent, cancellationToken);
        await sender.CloseAsync(cancellationToken);

        return messageToHook;
    }
}
