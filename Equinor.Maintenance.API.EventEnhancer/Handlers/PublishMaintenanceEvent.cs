using Equinor.Maintenance.API.EventEnhancer.EventSourcing;
using Equinor.Maintenance.API.EventEnhancer.Models;
using JetBrains.Annotations;
using MediatR;

namespace Equinor.Maintenance.API.EventEnhancer.Handlers;

public record PublishMaintenanceEventResult(MaintenanceEventHook? Data, int StatusCode);

public class PublishMaintenanceEventQuery(MaintenanceEventPublish maintenanceEventPublish) : IRequest<PublishMaintenanceEventResult>
{
    public MaintenanceEventPublish MaintenanceEventPublish { get; } = maintenanceEventPublish;
}

[UsedImplicitly]
public class PublishMaintenanceEvent(
    MessagePublisher publisher,
    SourceReactor sourceReactor,
    ILogger<PublishMaintenanceEvent> logger)
    : IRequestHandler<PublishMaintenanceEventQuery, PublishMaintenanceEventResult>
{
    public async Task<PublishMaintenanceEventResult> Handle(PublishMaintenanceEventQuery query,
                                                            CancellationToken cancellationToken)
    {
        var data = query.MaintenanceEventPublish.Data;
        var (sourceData, uri) = await sourceReactor.Enhance(data.Object, data.ObjectId);

        var messageToHook = await publisher.Publish(data.Event, sourceData, query.MaintenanceEventPublish.Id,
            query.MaintenanceEventPublish.Time, uri, data.ObjectId, cancellationToken);
        
        return new PublishMaintenanceEventResult(messageToHook, StatusCodes.Status201Created);
    }

    
}