using System.Text.Json;
using System.Text.Json.Serialization;
using SchoolMaster.Application.DTOs;

namespace SchoolMaster.Api.Converters;
// Creates an Optional<T> Object depending on the JSON
public class OptionalConverterFactory : JsonConverterFactory
{
    // Checks whether the type the JSON system is looking at is a generic type and compares to optional<> (a type that has <…> after its name).
    // Example of a generic type: Optional<int>, List<string>, Dictionary<int,string>.
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType &&
        typeToConvert.GetGenericTypeDefinition() == typeof(Optional<>);
    
    // then this is called which creates an instance of OptionalConverter<T> only when the JSON 
    // has a property type that matches <T>. if there is no property type CreateConverter doesn't run and just returns default(Optional<T>)
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
        // if the token is present but value is null, it builds new Optional<T>(null)
        // inside the struct, HasValue = true, value = null
        if (reader.TokenType == JsonTokenType.Null)
            return new Optional<T>(default);
        // else create struct with value of <T>
        return new Optional<T>(JsonSerializer.Deserialize<T>(ref reader, options));
    }
    // when your API sends a response back to the client,
    // and it reaches a property whose type is Optional<T>, it asks the converter (the OptionalConverter<T> we defined) to turn that .NET value into JSON.

    public override void Write(Utf8JsonWriter writer, Optional<T> value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            JsonSerializer.Serialize(writer, value.Value, options);
        else
            writer.WriteNullValue();
    }
}
// The serializer does NOT decide “keep the old value or replace it”.
// Its job ends at filling the Optional<T> struct with the correct HasValue/Value information.
