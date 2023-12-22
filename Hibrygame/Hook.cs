using System.Runtime.InteropServices.JavaScript;

namespace ClassLibrary1;

public class Hook : Piece
{
    public Hook(ColorEnum color) : base(color)
    {
        Color = color;
    }
    public List<Position> GetMovesHook(Board board, Position pos)
    {
        var possibleMoves = new List<Position>();
        var initialState = board.StartBoard();
        
        var initialPosition = initialState.FirstOrDefault(x => x.Equals(pos));

        if (initialPosition == null) return possibleMoves;
        
        for (var row = initialPosition.row + 1; row < 8; row++)
        {
            var newPosition = new Position(row, initialPosition.column);
            if (Common.IsValidMove(board, newPosition)) possibleMoves.Add(newPosition);
            break; 
        }
        
        for (var row = initialPosition.row - 1; row >= 0; row--)
        {
            var newPosition = new Position(row, initialPosition.column);
            if (Common.IsValidMove(board, newPosition)) possibleMoves.Add(newPosition);
            break; 
        }
        
        for (var col = initialPosition.column + 1; col < 8; col++)
        {
            var newPosition = new Position(initialPosition.row, col);
            if (Common.IsValidMove(board, newPosition)) possibleMoves.Add(newPosition);
            break; 
        }
        
        for (var col = initialPosition.column - 1; col >= 0; col--)
        {
            var newPosition = new Position(initialPosition.row, col);
            if (Common.IsValidMove(board, newPosition)) possibleMoves.Add(newPosition);
            break;
        }

        return possibleMoves;
    }
}
