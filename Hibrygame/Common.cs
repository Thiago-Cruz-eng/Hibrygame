namespace Hibrygame;

public static class Common
{
    private static bool IsInsideTheBoard(Position newPosition)
    {
        return newPosition.row >= 0 || newPosition.row < 8 ||
               newPosition.column >= 0 || newPosition.column < 8;
    }
    
    public static bool IsValidMove(Board board, Position newPosition)
    {
        if (!IsInsideTheBoard(newPosition)) return false;
        
        return board.positions[newPosition.row, newPosition.column].piece?.Type == null && 
               board.positions[newPosition.row, newPosition.column].piece?.Color != newPosition.squareColor;
    }

    public static List<Position> GetOpponentPositions(Board board, ColorEnum opponent)
    {
        var pos = new List<Position>();
        foreach (var position in board.positions)
        {
            if (position.piece?.Color == opponent)
                pos.Add(position);
        }

        return pos;
    }
}