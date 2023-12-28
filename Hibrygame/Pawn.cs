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
        var squares = 2;
        if (HasAlreadyOneMove) squares = 1;
        var direction = new List<Direction>();
        if (Color == ColorEnum.Black)
            direction =
            [
                Direction.South,
                Direction.SouthEast,
                Direction.SouthWest
            ];
        else
            direction =
            [
                Direction.North,
                Direction.NorthEast,
                Direction.NorthWest
            ];
        
        var moves = Move.MakeMove(board, pos, direction, squares);
        HasAlreadyOneMove = true;
        return moves;
    }
}