using Microsoft.AspNetCore.WebUtilities;

namespace Equinor.Maintenance.API.EventEnhancer.MaintenanceApiClient.Requests;

public class MaintenanceRecordsBuilder
{
    private const string Uri = "maintenance-api/maintenance-records/{0}/{1}";
    private static readonly Dictionary<string, string?> CommonQueryParams = new()
                                                                            {
                                                                                { "api-version", "v1" },
                                                                                { "include-attachments", "true" },
                                                                                { "include-status-details", "true" },
                                                                                { "include-created-by-details", "false" } 
                                                                            };

    public static string BuildActivityReportLookup(string recordId)
    {
        var queryParams = CommonQueryParams.Concat(new Dictionary<string, string?>
                                                      {
                                                          { "include-activities", "true" },
                                                          { "include-url-references", "true" }
                                                      });

        return QueryHelpers.AddQueryString(string.Format(Uri, "activity-reports", recordId), queryParams);
    }

    public static string BuildFailureReportLookup(string recordId)
    {
        var queryParams = CommonQueryParams.Concat(new Dictionary<string, string?>
                                                     {
                                                         { "include-activities", "true" },
                                                         { "include-url-references", "true" },
                                                         { "include-tag-details", "true" },
                                                         { "include-tasks", "true" },
                                                         { "include-additional-metadata", "true" }
                                                     });

        return QueryHelpers.AddQueryString(string.Format(Uri, "failure-reports", recordId), queryParams);
    }
}
