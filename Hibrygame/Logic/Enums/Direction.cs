using Hibrygame.Logic.Enums.EnumConverter;
using Newtonsoft.Json;

namespace Hibrygame.Enums;

[JsonConverter(typeof(EnumStringConverter<Direction>))] // Apply custom JSON converter to ColorEnum
public enum Direction
{
    North,
    South,
    East,
    West,
    NorthEast, 
    SouthEast, 
    NorthWest, 
    SouthWest,
}