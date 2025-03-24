using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.Messaging.ServiceBus;
using Equinor.Maintenance.API.EventEnhancer.Constants;
using Equinor.Maintenance.API.EventEnhancer.ExtensionMethods;
using Equinor.Maintenance.API.EventEnhancer.Handlers;
using Equinor.Maintenance.API.EventEnhancer.Models;

namespace Equinor.Maintenance.API.EventEnhancer.EventSourcing;

public class MessagePublisher(ServiceBusClient busClient)
{
    public async Task<MaintenanceEventHook> Publish(string @event,
                                                    JsonObject data,
                                                    string publishId,
                                                    string publishTime,
                                                    Uri sourceRequestUri,
                                                    string objectId,
                                                    CancellationToken cancellationToken)
    {
        var (type, sourcePart) = CheckEventAndSetProps(@event, sourceRequestUri.Segments[3].TrimEnd('/'));
        var messageToHook = new MaintenanceEventHook("1.0",
            type,
            publishId,
            publishTime,
            objectId,
            sourcePart,
            data);
        var maintenanceEvent = new ServiceBusMessage(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(messageToHook)));
        var properties = new Dictionary<string, object>()
        {
            { "filter-property-planning-plant-id", data.GetJsonObjectPropertyValue("planningPlantId").ToString() },
            { "filter-property-active-status-ids", data.GetJsonObjectPropertyValue("activeStatusIds").ToString() },
            { "filter-property-work-center-id", data.GetJsonObjectPropertyValue("workCenterId").ToString() },
            { "filter-property-planner-group-id", data.GetJsonObjectPropertyValue("plannerGroupId").ToString() }
        };
        var statuses = data.GetJsonObjectPropertyValueArray("statuses");
        foreach (var status in statuses)
        {
            var sa       = status.AsObject();
            var id       = sa.GetJsonObjectPropertyValue("statusId").ToString();
            var isActive = sa.GetJsonObjectPropertyValue("isActive").AsValue();
            properties.Add($"filter-property-status-{id}-is-active", isActive.ToString());
        }

        foreach (var property in properties)
        {
            maintenanceEvent.ApplicationProperties.Add(property);
        }

        var sender = busClient.CreateSender(Names.Topic);
        await sender.SendMessageAsync(maintenanceEvent, cancellationToken);
        await sender.CloseAsync(cancellationToken);
        return messageToHook;
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
            case "TECCOMPLETED":
                SetMetaData(ref type, ref sourcePart, $"{input}.technical-complete");

                break;
            case "CLOSED":
                SetMetaData(ref type, ref sourcePart, $"{input}.completed");

                break;
            case "INPROCESS":
                SetMetaData(ref type, ref sourcePart, $"{input}.in-process");

                break;
        }

        return (type, sourcePart);
    }

    private static void SetMetaData(ref string typeInput, ref string sourceInput, string input)
    {
        typeInput = string.Format(typeInput, input);
        sourceInput = string.Format(sourceInput, typeInput);
    }
}