using Hibrygame.Enums;

namespace Hibrygame;

public class Queen : Piece
{
    public Queen(ColorEnum color)
    {
        Color = color;
        Type = PieceEnum.Queen;
    }
}
