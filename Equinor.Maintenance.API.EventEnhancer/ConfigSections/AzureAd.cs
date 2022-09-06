using JetBrains.Annotations;
using Microsoft.Identity.Web;

namespace Equinor.Maintenance.API.EventEnhancer.ConfigSections;

public class AzureAd
{
    public string                   ClientId              { get; [UsedImplicitly] set; } = "";
    public string                   TenantId              { get; [UsedImplicitly] set; } = "";
    public string                   Instance              { get; [UsedImplicitly] set; } = "";
    public string                   ClientSecret          { get; [UsedImplicitly] set; } = "";
    public string[]                 AllowedWebHookOrigins { get; [UsedImplicitly] set; } = { };
    public CertificateDescription[] ClientCertificates      { get; [UsedImplicitly] set; } = { };
}
