using System.Text.Json.Nodes;

namespace Equinor.Maintenance.API.EventEnhancer.ExtensionMethods;

public static class JsonPropertyExtensions
{
    public static JsonValue GetJsonObjectPropertyValue(this JsonObject? jsonObject, string propertyName)
    {
        jsonObject.TryGetPropertyValue(propertyName, out JsonNode? value);
        return value.AsValue();
    }
    
    public static JsonArray GetJsonObjectPropertyValueArray(this JsonObject? jsonObject, string propertyName)
    {
        jsonObject.TryGetPropertyValue(propertyName, out JsonNode? value);
        return value.AsArray();
    }
}