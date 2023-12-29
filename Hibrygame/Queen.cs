namespace Hibrygame;

public class Queen : Piece
{
    public Queen(ColorEnum color)
    {
        Color = color;
        Type = PieceEnum.Queen;
    }

    public override List<Position> GetPossibleMove(Board board, Position pos)
    {
        var direction = new List<Direction>
        {
            Direction.North,
            Direction.South,
            Direction.East,
            Direction.West,
            Direction.SouthEast,
            Direction.SouthWest,
            Direction.NorthEast,
            Direction.NorthWest,
        };
        
        return Move.CalculatePossibleMove(board, pos, direction, 8);
    }
}