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
                    var newPosition = CalculateNewPosition(initialPosition, actualDir, i);
                    if (Common.IsValidMove(board, newPosition))
                        possibleMoves.Add(newPosition);
                    break;
                }
            }
            return possibleMoves;
        }
    }
    private static Position CalculateNewPosition(Position initialPosition, Direction direction, int steps)
    {
        return direction switch
        {
            Direction.North => new Position(initialPosition.row + steps, initialPosition.column),
            Direction.South => new Position(initialPosition.row - steps, initialPosition.column),
            Direction.East => new Position(initialPosition.row, initialPosition.column + steps),
            Direction.West => new Position(initialPosition.row, initialPosition.column - steps),
            Direction.NorthEast => new Position(initialPosition.row + steps, initialPosition.column + steps),
            Direction.SouthEast => new Position(initialPosition.row - steps, initialPosition.column + steps),
            Direction.NorthWest => new Position(initialPosition.row + steps, initialPosition.column - steps),
            Direction.SouthWest => new Position(initialPosition.row - steps, initialPosition.column - steps),
            _ => initialPosition 
        };
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