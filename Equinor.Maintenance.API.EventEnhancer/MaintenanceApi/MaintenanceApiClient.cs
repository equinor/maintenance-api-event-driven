using System.Text.Json.Nodes;
using Equinor.Maintenance.API.EventEnhancer.ConfigSections;
using Microsoft.Identity.Web;
using RestSharp;
using RestSharp.Authenticators;

namespace Equinor.Maintenance.API.EventEnhancer.MaintenanceApi;

public class MaintenanceApiClient
{
    private readonly RestClient _restClient;

    public MaintenanceApiClient(ITokenAcquisition tokenAcquisition, IConfiguration config)
    {
        _restClient
            = new RestClient(new RestClientOptions(new Uri(config.GetConnectionString(nameof(ConnectionStrings.MaintenanceApi))))
                             {
                                 FollowRedirects = false
                             })
              .AddDefaultQueryParameter("api-version", "v1")
              .UseJson();

        _restClient.Authenticator = new MaintenanceApiCertificateAuthenticatior(tokenAcquisition, $"{config["MaintenanceApiClientId"]}/.default");
    }

    public async Task<RestResponse<JsonObject>> LookupFailureReport(string recordId, CancellationToken ct)
    {
        var message = new RestRequest($"maintenance-records/failure-reports/{recordId}")
                      .AddQueryParameter("include-attachments", "true")
                      .AddQueryParameter("include-status-details", "true")
                      .AddQueryParameter("include-created-by-details", "false")
                      .AddQueryParameter("include-activities", "true")
                      .AddQueryParameter("include-tag-details", "true")
                      .AddQueryParameter("include-tasks", "true")
                      .AddQueryParameter("include-additional-metadata", "true");

        return await _restClient.ExecuteAsync<JsonObject>(message, cancellationToken: ct);
    }

    public async Task<RestResponse<JsonObject>> LookupCorrectiveWorkOrder(string workOrderId, CancellationToken ct)
    {
        var message = new RestRequest($"work-orders/corrective-work-order/{workOrderId}")
                      .AddQueryParameter("include-attachments", "true")
                      .AddQueryParameter("include-status-details", "true")
                      .AddQueryParameter("include-maintenance-records", "true")
                      .AddQueryParameter("include-tag-details", "true")
                      .AddQueryParameter("include-technical-feedback", "true")
                      .AddQueryParameter("include-operations", "true")
                      .AddQueryParameter("include-materials", "true")
                      .AddQueryParameter("include-related-tags", "true");

        return await _restClient.ExecuteAsync<JsonObject>(message, cancellationToken: ct);
    }

    public async Task<RestResponse<JsonObject>> FollowRedirect(string resource, CancellationToken ct)
    {
        var message = new RestRequest(resource);

        return await _restClient.ExecuteAsync<JsonObject>(message, cancellationToken: ct);
    }
}

public class MaintenanceApiCertificateAuthenticatior : AuthenticatorBase
{
    private readonly ITokenAcquisition _tokenAcquisition;
    private readonly string _scope;

    public MaintenanceApiCertificateAuthenticatior(ITokenAcquisition tokenAcquisition, string scope) : base("")
    {
        _tokenAcquisition = tokenAcquisition;
        _scope            = scope;
    }

    protected override async ValueTask<Parameter> GetAuthenticationParameter(string accessToken)
        => new HeaderParameter(KnownHeaders.Authorization, $"Bearer {await _tokenAcquisition.GetAccessTokenForAppAsync(_scope)}"); //tokenaqcuistion has caching
}
