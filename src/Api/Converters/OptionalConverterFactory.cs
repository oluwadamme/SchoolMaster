using System.Text.Json;
using System.Text.Json.Serialization;
using SchoolMaster.Application.DTOs;

namespace SchoolMaster.Api.Converters;

public class OptionalConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType &&
        typeToConvert.GetGenericTypeDefinition() == typeof(Optional<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var innerType = typeToConvert.GetGenericArguments()[0];
        return (JsonConverter)Activator.CreateInstance(
            typeof(OptionalConverter<>).MakeGenericType(innerType))!;
    }
}

public class OptionalConverter<T> : JsonConverter<Optional<T>>
{
    // Must be true so our Read method is called even when the JSON token is null.
    // Without this, System.Text.Json skips the converter for null tokens and returns
    // default(Optional<T>) — which has HasValue = false, making null indistinguishable
    // from absent.
    public override bool HandleNull => true;

    public override Optional<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return new Optional<T>(default);

        return new Optional<T>(JsonSerializer.Deserialize<T>(ref reader, options));
    }

    public override void Write(Utf8JsonWriter writer, Optional<T> value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            JsonSerializer.Serialize(writer, value.Value, options);
        else
            writer.WriteNullValue();
    }
}
