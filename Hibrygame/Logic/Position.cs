using Hibrygame.Enums;

namespace Hibrygame;

public class Position(int row, int column)
{
    public ColorEnum squareColor { get; set; } = ColorEnum.None;
    public Piece? piece { get; set; }
    public int row { get; set; } = row;
    public int column { get; set; } = column;
}
