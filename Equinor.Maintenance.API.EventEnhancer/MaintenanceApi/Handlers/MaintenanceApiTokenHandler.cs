using System.Net.Http.Headers;
using Microsoft.Identity.Web;

namespace Equinor.Maintenance.API.EventEnhancer.MaintenanceApi.Handlers;

public class MaintenanceApiTokenHandler(ITokenAcquisition tokenAcquisition, IConfiguration config) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await tokenAcquisition.GetAccessTokenForAppAsync($"{config["MaintenanceApiClientId"]}/.default",
            tokenAcquisitionOptions: new TokenAcquisitionOptions { CancellationToken = cancellationToken });
        request.Headers.Authorization = new AuthenticationHeaderValue(Microsoft.Identity.Web.Constants.Bearer, token);
        return await base.SendAsync(request, cancellationToken);
    }
}