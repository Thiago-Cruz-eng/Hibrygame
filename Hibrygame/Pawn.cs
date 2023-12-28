namespace Hibrygame;

public class Pawn : Piece
{
    public Pawn(ColorEnum color)
    {
        Color = color;
        Type = PieceEnum.Pawn;
    }

    public bool HasAlreadyOneMove { get; set; }
    
    public List<Position> GetMovesPawn(Board board, Position pos)
    {
        var squares = HasAlreadyOneMove ? 1 : 2;
        var direction = Color == ColorEnum.Black
            ? new List<Direction> { Direction.South, Direction.SouthEast, Direction.SouthWest }
            : new List<Direction> { Direction.North, Direction.NorthEast, Direction.NorthWest };
        
        var moves = Move.CalculatePossibleMove(board, pos, direction, squares);
        HasAlreadyOneMove = true;
        return moves;
    }
}