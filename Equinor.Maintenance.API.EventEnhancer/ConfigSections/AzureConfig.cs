namespace Equinor.Maintenance.API.EventEnhancer.ConfigSections;

public class AzureConfig
{

    public string? ClientId     { get; set; }
    public string? ClientSecret { get; set; }
    public string[]? AllowedWebHookOrigins { get; set; }
}
