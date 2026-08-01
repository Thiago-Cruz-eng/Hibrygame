using Hibrygame.Enums;

namespace Hibrygame;

public class Pawn : Piece
{
    public Pawn(ColorEnum color)
    {
        Color = color;
        Type = PieceEnum.Pawn;
    }
}
