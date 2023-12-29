namespace Hibrygame;

public class Rook : Piece
{
    public Rook(ColorEnum color)
    {
        Color = color;
        Type = PieceEnum.Rook;
    }
    
    public override List<Position> GetPossibleMove(Board board, Position pos)
    {
        var direction = new List<Direction>
        {
            Direction.North,
            Direction.South,
            Direction.East,
            Direction.West,
        };
        
        return Move.CalculatePossibleMove(board, pos, direction, 8);
    }
}
