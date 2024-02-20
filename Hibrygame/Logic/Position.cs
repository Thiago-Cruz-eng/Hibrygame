using Hibrygame.Enums;

namespace Hibrygame;

public class Position(int row, int column)
{
    public ColorEnum SquareColor { get; set; } = ColorEnum.None;
    public Piece? Piece { get; set; }
    public int Row { get; set; } = row;
    public int Column { get; set; } = column;
}
