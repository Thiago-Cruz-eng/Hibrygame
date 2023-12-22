namespace ClassLibrary1;

public class King : Piece
{
    public King(ColorEnum color) : base(color)
    {
        Color = color;
    }
    
    public List<Position> GetMovesKing(Board board, Position pos)
    {
        var possibleMoves = new List<Position>();
        var initialState = board.StartBoard();
        
        var initialPosition = initialState.FirstOrDefault(x => x.Equals(pos));

        if (initialPosition == null) return possibleMoves;
        
        for (var row = initialPosition.row + 1; row < 1; row++)
        {
            var newPosition = new Position(row, initialPosition.column);
            if (Common.IsValidMove(board, newPosition)) possibleMoves.Add(newPosition);
            break; 
        }
        
        for (var row = initialPosition.row + 1 + initialPosition.column + 1; row < 1; row++)
        {
            var newPosition = new Position(row, initialPosition.column);
            if (Common.IsValidMove(board, newPosition)) possibleMoves.Add(newPosition);
            break; 
        }
        
        for (var row = initialPosition.row - 1; row < 1; row++)
        {
            var newPosition = new Position(row, initialPosition.column);
            if (Common.IsValidMove(board, newPosition)) possibleMoves.Add(newPosition);
            break; 
        }
        
        for (var row = initialPosition.row - 1 + initialPosition.column + 1; row < 1; row++)
        {
            var newPosition = new Position(row, initialPosition.column);
            if (Common.IsValidMove(board, newPosition)) possibleMoves.Add(newPosition);
            break; 
        }
        
        for (var row = initialPosition.column + 1; row < 1; row++)
        {
            var newPosition = new Position(row, initialPosition.column);
            if (Common.IsValidMove(board, newPosition)) possibleMoves.Add(newPosition);
            break; 
        }
        
        for (var row = initialPosition.column + 1 + initialPosition.row - 1; row < 1; row++)
        {
            var newPosition = new Position(row, initialPosition.column);
            if (Common.IsValidMove(board, newPosition)) possibleMoves.Add(newPosition);
            break; 
        }
        
        for (var row = initialPosition.column - 1; row < 1; row++)
        {
            var newPosition = new Position(row, initialPosition.column);
            if (Common.IsValidMove(board, newPosition)) possibleMoves.Add(newPosition);
            break; 
        }
        
        for (var row = initialPosition.column + 1 + initialPosition.row + 1; row < 1; row++)
        {
            var newPosition = new Position(row, initialPosition.column);
            if (Common.IsValidMove(board, newPosition)) possibleMoves.Add(newPosition);
            break; 
        }
        
        return possibleMoves;
    }
}