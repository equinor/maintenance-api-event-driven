namespace Equinor.Maintenance.API.EventEnhancer.Constants;

public static class Names
{
    public const string WebHookRequestHeader = "Webhook-Request-Origin";
    public const string WebHookAllowHeader = "Webhook-Allowed-Origin";
    public const string Topic = "maintenance-events";
    public const string MainteanceApi = "MaintenanceApiServiceName";
}

public static class Policy
{
    public const string Publish = "PublishPolicy";
    public const string WebHookOrigin = "WebHookOriginPolicy";
}

public static class Role
{
    public const string Publish = nameof(Publish);
}
