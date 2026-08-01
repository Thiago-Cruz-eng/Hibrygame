using Hibrygame.Enums;

namespace Hibrygame;

public class Knight : Piece
{
    public Knight(ColorEnum color)
    {
        Color = color;
        Type = PieceEnum.Knight;
    }
}
