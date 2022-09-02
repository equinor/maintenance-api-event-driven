using Microsoft.AspNetCore.WebUtilities;

namespace Equinor.Maintenance.API.EventEnhancer.MaintenanceRequests;

public class MaintenanceRecordsRequestBuilder
{
    private const string Uri = "maintenance-records/{0}/{1}";
    private static readonly Dictionary<string, string?> CommonQueryParams = new()
                                                                      {
                                                                          { "api-version", "v1" },
                                                                          { "include-attachments", "true" },
                                                                          { "include-status-details", "true" },
                                                                          { "include-created-by-details", "false" } //note double check this value
                                                                      };

    public static string BuildActivityReportLookup(string objectId)
    {
        var activityParams = CommonQueryParams.Concat(new Dictionary<string, string?>
                                                       {
                                                           { "include-activities", "true" },
                                                           { "include-references", "true" }
                                                       });

        return QueryHelpers.AddQueryString(string.Format(Uri, "activity-reports", objectId), activityParams);
    }
}
