namespace Hibrygame;

public class Rook : Piece
{
    public Rook(ColorEnum color)
    {
        Color = color;
        Type = PieceEnum.Rook;
    }
    public List<Position> GetMovesHook(Board board, Position pos)
    {
        var direction = new List<Direction>
        {
            Direction.North,
            Direction.South,
            Direction.East,
            Direction.West,
        };
        
        return Move.MakeMove(board, pos, direction, 8);
    }
}
