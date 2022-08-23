using JetBrains.Annotations;

namespace Equinor.Maintenance.API.EventEnhancer.ConfigSections;

public class AzureConfig
{
    public string   ClientId              { get; [UsedImplicitly] set; } = "";
    public string   ClientSecret          { get; [UsedImplicitly] set; } = "";
    public string[] AllowedWebHookOrigins { get; [UsedImplicitly] set; } = { };
}
