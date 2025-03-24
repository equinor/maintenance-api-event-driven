using System.Text.Json;
using System.Text.Json.Nodes;
using Azure;
using Equinor.Maintenance.API.EventEnhancer.Constants;
using Equinor.Maintenance.API.EventEnhancer.ExtensionMethods;
using Equinor.Maintenance.API.EventEnhancer.MaintenanceApi.Clients;

namespace Equinor.Maintenance.API.EventEnhancer.EventSourcing;

public class SourceReactor(IHttpClientFactory factory)
{
    public async Task<(JsonObject, Uri)> Enhance(string eventType, string objectId)
    {
        var request = eventType switch
        {
            "BUS2007" => EnhanceWorkOrder(objectId),
            "BUS2038" => EnhanceWorkOrder("notimplemented"),
            _ => throw new ArgumentOutOfRangeException(nameof(eventType))
        };
        return await request;
    }

// rewrite this method to be more generic (work for any type?) and to also construct objects from an allowlist
    private async Task<(JsonObject, Uri)> EnhanceWorkOrder(string objectId)
    {
        var workOrderClient   = new WorkOrderClient(factory.CreateClient(Names.MainteanceApi));
        var workOrderResponse = await workOrderClient.WorkOrderExistsAsync([objectId.TrimStart('0')]);

        if (!workOrderResponse.IsSuccessStatusCode)
            throw new RequestFailedException((int)workOrderResponse.StatusCode, "Work order not found");
        var workOrderJson = await workOrderResponse.Content.ReadFromJsonAsync<JsonArray>();

        if (workOrderJson is not { Count: > 0 })
            throw new RequestFailedException((int)workOrderResponse.StatusCode, "Work order not found");

        var workOrderType = workOrderJson.First().AsObject();
        var workOrderLookupResponse =
            await workOrderClient.Lookup(
                workOrderType
                    .GetJsonObjectPropertyObject("_links")
                    .GetJsonObjectPropertyValue("self")
                    .ToString());

        var lightWorkOrder = await workOrderLookupResponse.Content.ReadFromJsonAsync<LightWorkOrder>();

        var data = new JsonObject
        {
            ["workOrderId"] = lightWorkOrder.WorkOrderId,
            ["planningPlant"] = lightWorkOrder.PlanningPlant,
            ["activeStatusIds"] = lightWorkOrder.ActiveStatusIds,
            ["workCenterId"] = lightWorkOrder.WorkCenterId,
            ["plannerGroupId"] = lightWorkOrder.PlannerGroupId
        };
        return (data, workOrderLookupResponse.RequestMessage.RequestUri);
    }
}

public record LightWorkOrder(string WorkOrderId, string PlanningPlant, string ActiveStatusIds, string WorkCenterId, string PlannerGroupId);