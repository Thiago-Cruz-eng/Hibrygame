using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Orchestrator.Infra.Utils;

public class EnumStringConverter<TEnum> : StringEnumConverter where TEnum : Enum
{
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        if (value is TEnum)
        {
            var enumValue = (TEnum)value;
            var enumString = enumValue.ToString().ToLower(); 
            writer.WriteValue(enumString);
        }
        else
        {
            base.WriteJson(writer, value, serializer);
        }
    }
}