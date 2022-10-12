using Microsoft.AspNetCore.WebUtilities;

namespace Equinor.Maintenance.API.EventEnhancer.MaintenanceApiClient.Requests;

public class WorkorderBuilder
{
    private const string Uri = "work-orders/{0}/{1}";
    private static readonly Dictionary<string, string?> CommonQueryParams = new()
                                                                             {
                                                                                 {"api-version", "v1"},
                                                                                 {"include-operations", "true"},
                                                                                 {"include-materials", "true"},
                                                                                 {"include-attachments", "true"},
                                                                                 {"include-status-details", "true"},
                                                                                 {"include-related-tags", "true"}
                                                                             };

    public static string BuildCorrectiveLookup(string workOrderId)
    {
        var queryParams = CommonQueryParams.Concat(new Dictionary<string, string?>
                                                             {
                                                                 { "include-maintenance-records", "true" },
                                                                 { "include-tag-details", "true" },
                                                                 { "include-technical-feedback", "true" }
                                                             });
        return QueryHelpers.AddQueryString(string.Format(Uri, "corrective-work-orders", workOrderId), queryParams);
    }
    public static string BuildSasChangeLookup(string workOrderId)
    {
        var queryParams = CommonQueryParams.Concat(new Dictionary<string, string?>
                                                             {
                                                                 { "include-tag-details", "true" }
                                                             });
        return QueryHelpers.AddQueryString(string.Format(Uri, "sas-change-work-orders", workOrderId), queryParams);
    }
    public static string BuildProjectLookup(string workOrderId)
    {
        var queryParams = CommonQueryParams.Concat(new Dictionary<string, string?>
                                                             {
                                                                 { "include-maintenance-records", "true" },
                                                                 { "include-tag-details", "true" },
                                                                 { "include-technical-feedback", "true" }
                                                             });
        return QueryHelpers.AddQueryString(string.Format(Uri, "project-work-orders", workOrderId), queryParams);
    }


}
