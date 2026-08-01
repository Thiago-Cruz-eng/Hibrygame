using Hibrygame.Enums;

namespace Hibrygame;

public class Rook : Piece
{
    public Rook(ColorEnum color)
    {
        Color = color;
        Type = PieceEnum.Rook;
    }
}
