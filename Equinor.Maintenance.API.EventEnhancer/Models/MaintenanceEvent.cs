using System.Net.Mime;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Equinor.Maintenance.API.EventEnhancer.Models;

public record MaintenanceEventBase([property: JsonPropertyName("specversion")]string Specversion, 
                                   [property: JsonPropertyName("type")]string Type, 
                                   [property: JsonPropertyName("id")]string Id, 
                                   [property: JsonPropertyName("time")]string Time);

// ---- incoming
public record ObjectData(
    string ObjectId,
    [property: JsonPropertyName("objectType")] string Object,
    [property: JsonPropertyName("event")] string Event);

// ---- incoming
public record MaintenanceEventPublish(string Specversion, string Type, string Id, string Time, ObjectData Data)
    : MaintenanceEventBase(Specversion, Type, Id, Time);

// ---- outgoing to servicebus
public record MaintenanceEventHook(
       string Specversion,
        string Type,
        string Id,
        string Time,
        [property: JsonPropertyName("subject")]string Subject,
        [property: JsonPropertyName("source")]string Source,
        JsonObject? Data,
        [property: JsonPropertyName("datacontenttype")] string DataContent = MediaTypeNames.Application.Json)
    : MaintenanceEventBase(Specversion, Type, Id, Time);
