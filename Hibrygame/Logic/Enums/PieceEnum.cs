using System.Runtime.Serialization;
using Hibrygame.Logic.Enums.EnumConverter;
using Newtonsoft.Json;

namespace Hibrygame.Enums;

[JsonConverter(typeof(EnumStringConverter<PieceEnum>))]
public enum PieceEnum
{
    [EnumMember(Value = "pawn")]
    Pawn,
    [EnumMember(Value = "bishop")]
    Bishop,
    [EnumMember(Value = "knight")]
    Knight,
    [EnumMember(Value = "rook")]
    Rook,
    [EnumMember(Value = "queen")]
    Queen,
    [EnumMember(Value = "king")]
    King,
    [EnumMember(Value = "none")]
    None
}
