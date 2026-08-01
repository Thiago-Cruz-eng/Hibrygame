using Hibrygame.Enums;

namespace Hibrygame;

public class Bishop : Piece
{
    public Bishop(ColorEnum color)
    {
        Color = color;
        Type = PieceEnum.Bishop;
    }
}
