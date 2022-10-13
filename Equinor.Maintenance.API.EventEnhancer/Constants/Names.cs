namespace Equinor.Maintenance.API.EventEnhancer.Constants;

public class Names
{
    public const string WebHookRequestHeader = "Webhook-Request-Origin";
    public const string WebHookAllowHeader = "Webhook-Allowed-Origin";
    public const string Topic = "maintenance-events";
    public const string MainteanceApi = "MaintenanceApiServiceName";
}

public class Policy
{
    public const string Publish = "PublishPolicy";
    public const string WebHookOrigin = "WebHookOriginPolicy";
}

public class Role
{
    public const string Publish = nameof(Publish);
}
