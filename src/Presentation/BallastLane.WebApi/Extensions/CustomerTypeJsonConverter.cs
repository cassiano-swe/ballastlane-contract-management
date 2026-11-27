using System.Text.Json;
using System.Text.Json.Serialization;
using BallastLane.Domain.Enums;
namespace BallastLane.WebApi.Extensions;
public sealed class CustomerTypeJsonConverter : JsonConverter<CustomerType>
{
    public override CustomerType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String) throw new JsonException("Customer type must be a string.");
        return reader.GetString() switch { "Individual" => CustomerType.Individual, "Company" => CustomerType.Company, _ => throw new JsonException("Invalid customer type.") };
    }
    public override void Write(Utf8JsonWriter writer, CustomerType value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
}
