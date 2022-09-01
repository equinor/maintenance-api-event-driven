using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Equinor.Maintenance.API.EventEnhancer.Models;

public record MaintenanceEventBase(string Specversion, string Type, string Id, string Time);

public record ObjectData(string ObjectId, string Object, [property: JsonPropertyName("event")] string Event);

public record MaintenanceEventPublish(string Specversion, string Type, string Id, string Time, ObjectData Data)
    : MaintenanceEventBase(Specversion, Type, Id, Time);

public record MaintenanceEventHook
    (
        string Specversion,
        string Type,
        string Id,
        string Time,
        string Subject,
        string Source,
        [property: JsonPropertyName("datacontenttype")] string DataContent,
        string Data)
    : MaintenanceEventBase(Specversion, Type, Id, Time);
