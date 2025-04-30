using System.Text.Json.Nodes;

namespace Equinor.Maintenance.API.EventEnhancer.ExtensionMethods;

public static class JsonPropertyExtensions
{
    public static T GetJsonEntity<T>(this JsonObject? jsonObject, string propertyName) where T : JsonNode
    {
        if (jsonObject != null && jsonObject.TryGetPropertyValue(propertyName, out JsonNode? value))
        {
            return value as T ?? throw new InvalidCastException($"Property {propertyName} is not of type {typeof(T).Name}");
        }

        throw new PropertyNotFoundException(propertyName);
    }

    public static JsonValue GetJsonObjectPropertyValue(this JsonObject? jsonObject, string propertyName) 
    => jsonObject.GetJsonEntity<JsonValue>(propertyName);

    public static JsonObject GetJsonObjectPropertyObject(this JsonObject? jsonObject, string propertyName) 
    => jsonObject.GetJsonEntity<JsonObject>(propertyName);

    public static JsonArray GetJsonObjectPropertyValueArray(this JsonObject? jsonObject, string propertyName) 
    => jsonObject.GetJsonEntity<JsonArray>(propertyName);
}

public class PropertyNotFoundException(string propertyName)
    : Exception($"Property {propertyName} not found");