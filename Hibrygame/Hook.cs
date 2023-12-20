using System.Runtime.InteropServices.JavaScript;

namespace ClassLibrary1;

public class Hook : Piece
{
    public List<Position> GetMovesHook(Board board, Position pos)
    {
        var possibleMoves = new List<Position>();
        var initialState = board.StartBoard();
        
        var initialPosition = initialState.FirstOrDefault(x => x.Equals(pos));

        if (initialPosition == null) return possibleMoves;
        
        for (var row = initialPosition.row + 1; row < 8; row++)
        {
            var newPosition = new Position(row, initialPosition.column);
            if (IsValidMove(board, newPosition)) possibleMoves.Add(newPosition);
            break; 
        }
        
        for (var row = initialPosition.row - 1; row >= 0; row--)
        {
            var newPosition = new Position(row, initialPosition.column);
            if (IsValidMove(board, newPosition)) possibleMoves.Add(newPosition);
            break; 
        }
        
        for (var col = initialPosition.column + 1; col < 8; col++)
        {
            var newPosition = new Position(initialPosition.row, col);
            if (IsValidMove(board, newPosition)) possibleMoves.Add(newPosition);
            break; 
        }
        
        for (var col = initialPosition.column - 1; col >= 0; col--)
        {
            var newPosition = new Position(initialPosition.row, col);
            if (IsValidMove(board, newPosition)) possibleMoves.Add(newPosition);
            break;
        }

        return possibleMoves;
    }

    private bool IsInsideTheBoard(Board board, Position newPosition)
    {
        return newPosition.row < 0 || newPosition.row >= 8 ||
               newPosition.column < 0 || newPosition.column >= 8;
    }
    
    private bool IsValidMove(Board board, Position newPosition)
    {
        if (!IsInsideTheBoard(board, newPosition)) return false;
        
        return board.positions[newPosition.row, newPosition.column].squareColor == ColorEnum.None;
    }
}
