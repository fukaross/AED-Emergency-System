using System.Text.Json;
using System.Text.Json.Serialization;

namespace AedBackend.Core.DTOs.Admin;

[JsonConverter(typeof(OptionalValueJsonConverterFactory))]
public readonly record struct OptionalValue<T>(bool HasValue, T? Value);

public sealed class OptionalValueJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsGenericType
            && typeToConvert.GetGenericTypeDefinition() == typeof(OptionalValue<>);
    }

    public override JsonConverter CreateConverter(
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var valueType = typeToConvert.GetGenericArguments()[0];
        var converterType = typeof(OptionalValueJsonConverter<>).MakeGenericType(valueType);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

public sealed class OptionalValueJsonConverter<T> : JsonConverter<OptionalValue<T>>
{
    public override OptionalValue<T> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = JsonSerializer.Deserialize<T>(ref reader, options);
        return new OptionalValue<T>(true, value);
    }

    public override void Write(
        Utf8JsonWriter writer,
        OptionalValue<T> value,
        JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value.Value, options);
    }
}
