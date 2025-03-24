using Equinor.Maintenance.API.EventEnhancer.MaintenanceApi.Requests;

namespace Equinor.Maintenance.API.EventEnhancer.MaintenanceApi.Clients;

public class WorkOrderClient(HttpClient client) : MaintenanceApiBase(client)
{
    public async Task<HttpResponseMessage> WorkOrderExistsAsync(string[] workOrderIds)
    {
        var uri      = WorkorderBuilder.BuildWorkOrdersExist(workOrderIds);
        return await HttpClient.GetAsync(uri);
    }

    public async Task<HttpResponseMessage> Lookup(string link)
    {
        return await HttpClient.GetAsync(link);
    }
}