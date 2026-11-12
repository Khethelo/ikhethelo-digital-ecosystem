using System.Text.Json;
using System.Text.Json.Serialization;

namespace IKhethelo.Digital.Api.Http;

public sealed class StrictEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String && Enum.TryParse<T>(reader.GetString(), false, out var value) &&
            Enum.IsDefined(value) && value.ToString() == reader.GetString()) return value;
        throw new JsonException("validation:status");
    }
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
}
