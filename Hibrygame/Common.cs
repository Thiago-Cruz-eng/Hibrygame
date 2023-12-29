using System.Net;

namespace Hibrygame;

public static class Common
{
    public static bool IsInsideTheBoard(Position newPosition)
    {
        return newPosition.row is >= 0 and < 8 &&
                 newPosition.column is >= 0 and < 8;
    }
    
    public static bool IsValidMove(Board board, Position newPosition, Position initialPosition)
    {
        if (!IsInsideTheBoard(newPosition)) return false;
        
        if (board.positions[newPosition.row, newPosition.column].piece?.Type == null) return true;
        return board.positions[newPosition.row, newPosition.column].piece?.Color != initialPosition.piece?.Color;
    }

    public static List<Position> GetOpponentPositions(Board board, ColorEnum opponent)
    {
        var pos = new List<Position>();
        _ = opponent == ColorEnum.Black ? opponent = ColorEnum.White : opponent;
        
        foreach (var position in board.positions)
        {
            if (position.piece?.Color == opponent)
                pos.Add(position);
        }

        return pos;
    }
    public static List<Position> GetByColorPositions(Board board, ColorEnum color)
    {
        var pos = new List<Position>();
        
        foreach (var position in board.positions)
        {
            if (position.piece?.Color == color)
                pos.Add(position);
        }

        return pos;
    }
    
}