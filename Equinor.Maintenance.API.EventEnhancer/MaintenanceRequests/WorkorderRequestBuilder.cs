using Microsoft.AspNetCore.WebUtilities;

namespace Equinor.Maintenance.API.EventEnhancer.MaintenanceRequests;

public class WorkorderRequestBuilder
{
    private const string Uri = "work-orders/{0}/{1}";
    private readonly Dictionary<string, string?> _commonQueryParams = new()
                                                                     {
                                                                         {"api-version", "v1"},
                                                                         {"include-operations", "true"},
                                                                         {"include-materials", "true"},
                                                                         {"include-attachments", "true"},
                                                                         {"include-status", "true"},
                                                                         {"include-related-tags", "true"}
                                                                     };

    public string BuildCorrectiveLookup(string workOrderId)
    {
        var uriWithValues = string.Format(Uri, "corrective-work-orders", workOrderId);
        var correctiveQueryParams = _commonQueryParams.Concat(new Dictionary<string, string?>
                                                             {
                                                                 { "include-maintenance-records", "true" },
                                                                 { "include-tag-details", "true" }
                                                             });
        return QueryHelpers.AddQueryString(uriWithValues, correctiveQueryParams);
    }
}
