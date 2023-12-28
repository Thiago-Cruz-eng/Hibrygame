namespace Hibrygame;

public class Bishop : Piece
{
    public Bishop(ColorEnum color)
    {
        Color = color;
        Type = PieceEnum.Bishop;
    }
    
    public List<Position> GetMovesBIshop(Board board, Position pos)
    {
        var direction = new List<Direction>
        {
            Direction.SouthEast,
            Direction.SouthWest,
            Direction.NorthEast,
            Direction.NorthWest,
        };
        
        return Move.MakeMove(board, pos, direction, 8);
    }
}