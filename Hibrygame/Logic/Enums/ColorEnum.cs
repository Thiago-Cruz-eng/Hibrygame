using System.Runtime.Serialization;
using Hibrygame.Logic.Enums.EnumConverter;
using Newtonsoft.Json;

namespace Hibrygame.Enums;

[JsonConverter(typeof(EnumStringConverter<ColorEnum>))]
public enum ColorEnum
{
    [EnumMember(Value = "black")]
    Black,
    [EnumMember(Value = "bhite")]
    White,
    [EnumMember(Value = "white")]
    None
}