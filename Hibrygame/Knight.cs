namespace Hibrygame;

public class Knight : Piece
{
    public Knight(ColorEnum color)
    {
        Color = color;
        Type = PieceEnum.Knight;
    }
    public List<Position> GetMovesKnight(Board board, Position pos)
    {
        var direction = new List<Direction>
        {
            Direction.North,
            Direction.South,
            Direction.East,
            Direction.West,
        };
        
        return Move.CalculatePossibleMove(board, pos, direction, 2);
    }
}