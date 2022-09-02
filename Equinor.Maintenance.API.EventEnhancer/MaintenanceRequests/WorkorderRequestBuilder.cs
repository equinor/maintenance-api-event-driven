using Microsoft.AspNetCore.WebUtilities;

namespace Equinor.Maintenance.API.EventEnhancer.MaintenanceRequests;

public class WorkorderRequestBuilder
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
        var uriWithValues = string.Format(Uri, "corrective-work-orders", workOrderId);
        var correctiveQueryParams = CommonQueryParams.Concat(new Dictionary<string, string?>
                                                             {
                                                                 { "include-maintenance-records", "true" },
                                                                 { "include-tag-details", "true" },
                                                                 { "include-technical-feedback", "true" }
                                                             });
        return QueryHelpers.AddQueryString(uriWithValues, correctiveQueryParams);
    }


}
