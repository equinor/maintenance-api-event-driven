namespace Equinor.Maintenance.API.EventEnhancer.MaintenanceApi.Clients;

public class MaintenanceApiBase(HttpClient httpClient)
{
    protected HttpClient HttpClient { get; } = httpClient;
}