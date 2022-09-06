using System.Net.Mime;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Equinor.Maintenance.API.EventEnhancer.Models;

public record MaintenanceEventBase(string Specversion, string Type, string Id, string Time);

public record ObjectData(
    string ObjectId,
    [property: JsonPropertyName("objectType")] string Object,
    [property: JsonPropertyName("event")] string Event);

public record MaintenanceEventPublish(string Specversion, string Type, string Id, string Time, ObjectData Data)
    : MaintenanceEventBase(Specversion, Type, Id, Time);

public record MaintenanceEventHook(
        string Specversion,
        string Type,
        string Id,
        string Time,
        string Subject,
        string Source,
        JsonObject? Data,
        [property: JsonPropertyName("datacontenttype")] string DataContent = MediaTypeNames.Application.Json)
    : MaintenanceEventBase(Specversion, Type, Id, Time);
