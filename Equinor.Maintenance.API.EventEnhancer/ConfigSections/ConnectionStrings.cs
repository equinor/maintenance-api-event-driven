using JetBrains.Annotations;

namespace Equinor.Maintenance.API.EventEnhancer.ConfigSections;

public class ConnectionStrings
{
    public string KeyVault         { get; [UsedImplicitly] set; } = "";
    public  string MaintenanceApi      { get; [UsedImplicitly] set; } = "";
    public  string ServiceBus          { get; [UsedImplicitly] set; } = "";
    public  string ApplicationInsights { get; [UsedImplicitly] set; } = "";
}
