namespace Hibrygame;

public static class Move
{
    public static List<Position> MakeMove(Board board, Position pos, List<Direction> dir, int squares)
    {
        {
            var possibleMoves = new List<Position>();
            var initialState = board.GetPositionsPlaced();
            var initialPosition = initialState.FirstOrDefault(x => x.row == pos.row && x.column == pos.column);

            if (initialPosition == null) return possibleMoves;

            foreach (var actualDir in dir)
            {
                for (var i = 1; i <= squares; i++)
                {
                    var newPosition = CalculateNewPosition(board,initialPosition, actualDir, i);
                    if (Common.IsValidMove(board, newPosition, initialPosition))
                        possibleMoves.Add(newPosition);
                }
            }
            return possibleMoves;
        }
    }
    private static Position CalculateNewPosition(Board board, Position initialPosition, Direction direction, int steps)
    {
        try
        {
            switch (direction)
            {
                case Direction.North:
                {
                    if (!Common.IsInsideTheBoard(new Position(initialPosition.row + steps, initialPosition.column)))
                        return initialPosition;
                    var newPosition = board.positions[initialPosition.row + steps, initialPosition.column];
                    if (!Common.IsValidMove(board, newPosition, initialPosition)) return initialPosition;
                    return newPosition;
                }

                case Direction.South:
                {
                    if (!Common.IsInsideTheBoard(new Position(initialPosition.row - steps, initialPosition.column)))
                        return initialPosition;
                    var newPosition = board.positions[initialPosition.row - steps, initialPosition.column];
                    if (!Common.IsValidMove(board, newPosition, initialPosition)) return initialPosition;
                    return newPosition;
                }

                case Direction.East:
                {
                    if (!Common.IsInsideTheBoard(new Position(initialPosition.row, initialPosition.column + steps)))
                        return initialPosition;
                    var newPosition = board.positions[initialPosition.row, initialPosition.column + steps];
                    if (!Common.IsValidMove(board, newPosition, initialPosition)) return initialPosition;
                    return newPosition;
                }

                case Direction.West:
                {
                    if (!Common.IsInsideTheBoard(new Position(initialPosition.row, initialPosition.column - steps)))
                        return initialPosition;
                    var newPosition = board.positions[initialPosition.row, initialPosition.column - steps];
                    if (!Common.IsValidMove(board, newPosition, initialPosition)) return initialPosition;
                    return newPosition;
                }

                case Direction.NorthEast:
                {           
                    if (!Common.IsInsideTheBoard(new Position(initialPosition.row + steps, initialPosition.column + steps)))
                        return initialPosition;
                    var newPosition = board.positions[initialPosition.row + steps, initialPosition.column + steps];
                    if (!Common.IsValidMove(board, newPosition, initialPosition)) return initialPosition;
                    return newPosition;
                }

                case Direction.SouthEast:
                {
                    if (!Common.IsInsideTheBoard(new Position(initialPosition.row - steps, initialPosition.column + steps)))
                        return initialPosition;
                    var newPosition = board.positions[initialPosition.row - steps, initialPosition.column + steps];
                    if (!Common.IsValidMove(board, newPosition, initialPosition)) return initialPosition;
                    return newPosition;
                }

                case Direction.NorthWest:
                {
                    if (!Common.IsInsideTheBoard(new Position(initialPosition.row + steps, initialPosition.column - steps)))
                        return initialPosition;
                    var newPosition = board.positions[initialPosition.row + steps, initialPosition.column - steps];
                    if (!Common.IsValidMove(board, newPosition, initialPosition)) return initialPosition;
                    return newPosition;
                }

                case Direction.SouthWest:
                {
                    if (!Common.IsInsideTheBoard(new Position(initialPosition.row - steps, initialPosition.column - steps)))
                        return initialPosition;
                    var newPosition = board.positions[initialPosition.row - steps, initialPosition.column - steps];
                    if (!Common.IsValidMove(board, newPosition, initialPosition)) return initialPosition;
                    return newPosition;
                }

                default:
                    return initialPosition;
            }
        }
        catch (IndexOutOfRangeException e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}

public enum Direction
{
    North,
    South,
    East,
    West,
    NorthEast, 
    SouthEast, 
    NorthWest, 
    SouthWest,
}