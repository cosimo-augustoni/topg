using System.Text.Json;
using System.Text.Json.Serialization;

namespace topg.Web.Client.Creator.Model;

/// <summary>JSON settings for everything the creator persists (IndexedDB and project files).</summary>
public static class CreatorJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        // Hand-edited or merged files may not have "type" first.
        AllowOutOfOrderMetadataProperties = true,
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options) ?? throw new JsonException($"JSON did not contain a {typeof(T).Name}.");

    /// <summary>Deep copy through JSON, e.g. for undo snapshots.</summary>
    public static T Clone<T>(T value) => Deserialize<T>(Serialize(value));
}
