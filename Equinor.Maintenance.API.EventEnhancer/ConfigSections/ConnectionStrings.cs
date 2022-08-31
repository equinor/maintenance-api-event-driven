using JetBrains.Annotations;

namespace Equinor.Maintenance.API.EventEnhancer.ConfigSections;

public class ConnectionStrings
{
    public string ServiceBus { get; [UsedImplicitly] set; } = "";
    public string ApplicationInsights { get; [UsedImplicitly] set; } = "";
}
