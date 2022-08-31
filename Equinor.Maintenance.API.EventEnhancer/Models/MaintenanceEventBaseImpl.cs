using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Equinor.Maintenance.API.EventEnhancer.Models;

internal record MaintenanceEventBase(string Specversion, string Type, string Id, string Time);

internal record ObjectData(string ObjectId, string Object, [property: JsonPropertyName("event")] string Event);

internal record MaintenanceEventPublish(string Specversion, string Type, string Id, string Time, ObjectData Data)
    : MaintenanceEventBase(Specversion, Type, Id, Time);

internal record MaintenanceEventHook
    (
        string Specversion,
        string Type,
        string Id,
        string Time,
        string Subject,
        string Source,
        [property: JsonPropertyName("datacontenttype")] string DataContent,
        JsonObject Data)
    : MaintenanceEventBase(Specversion, Type, Id, Time);
