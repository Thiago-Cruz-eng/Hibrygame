namespace Hibrygame;

public abstract class Piece
{
    public PieceEnum Type { get; set; } = PieceEnum.None;
    public ColorEnum Color { get; set; } = ColorEnum.None;

    public abstract List<Position> GetPossibleMove(Board board, Position pos);
}