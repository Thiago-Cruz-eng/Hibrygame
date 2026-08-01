using Hibrygame.Enums;

namespace Hibrygame;

public class King : Piece
{
    public King(ColorEnum color)
    {
        Color = color;
        Type = PieceEnum.King;
    }
}
