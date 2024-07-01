using Hibrygame.Enums;

namespace Hibrygame;

public class Pawn : Piece
{
    public Pawn(ColorEnum color)
    {
        Color = color;
        Type = PieceEnum.Pawn;
    }

    public override (List<Position> possibleMoves, Piece? actualPieceTrigger) GetPossibleMove(Board board, Position pos)
    {
        
         var squares = pos.Piece!.HasAlreadyOneMove ? 1 :  2;
        var direction = Color == ColorEnum.Black
            ? new List<Direction> { Direction.South, Direction.SouthEast, Direction.SouthWest }
            : new List<Direction> { Direction.North, Direction.NorthEast, Direction.NorthWest };
        
        var moves = Move.CalculatePossibleMove(board, pos, direction, squares);
        return moves;
    }
}