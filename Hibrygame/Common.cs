namespace Hibrygame;

public static class Common
{
    public static bool IsInsideTheBoard(Board board, Position newPosition)
    {
        return newPosition.row < 0 || newPosition.row >= 8 ||
               newPosition.column < 0 || newPosition.column >= 8;
    }
    
    public static bool IsValidMove(Board board, Position newPosition)
    {
        if (!IsInsideTheBoard(board, newPosition)) return false;
        
        return board.positions[newPosition.row, newPosition.column].squareColor == ColorEnum.None;
    }
}