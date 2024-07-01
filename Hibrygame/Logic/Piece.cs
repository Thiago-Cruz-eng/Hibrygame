using Hibrygame.Enums;

namespace Hibrygame;

public abstract class Piece
{
    public PieceEnum Type { get; set; } = PieceEnum.None;
    public ColorEnum Color { get; set; } = ColorEnum.None;
    public bool IsInCheckState { get; set; }
    public bool HasAlreadyOneMove { get; set; }

    public abstract (List<Position> possibleMoves, Piece? actualPieceTrigger) GetPossibleMove(Board board, Position pos);
}