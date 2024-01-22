using Hibrygame.Enums;

namespace Hibrygame;

public static class Move
{
    public static (List<Position> possibleMoves, Piece? actualPieceTrigger) CalculatePossibleMove(Board board, Position pos, List<Direction> dir, int squares)
    {
        var possibleMoves = new List<Position>();
        var knightPossibleMoves = new List<Position>();
        var initialState = board.GetPositionsPlaced();
        var initialPosition = initialState.FirstOrDefault(x => x.row == pos.row && x.column == pos.column);
        var isInCheckState = false;

        if (initialPosition == null) return (possibleMoves, null);
        foreach (var actualDir in dir)
        {
            var movesInSpecficDirection = new List<Position>();
            var previousEnemyPosition = new List<Position>();
            for (var i = 1; i <= squares; i++)
            {
                var newPosition = CalculateNewPosition(board, initialPosition, actualDir, i);
                    
                if(initialPosition.piece?.Type == PieceEnum.Pawn && 
                   (actualDir == Direction.NorthEast ||
                   actualDir == Direction.NorthWest ||
                   actualDir == Direction.SouthEast ||
                   actualDir == Direction.SouthWest) &&
                   newPosition.piece?.Color == null)
                    break;
                
                if (initialPosition.piece?.Type == PieceEnum.Knight && i == squares)
                {
                    newPosition.piece = new Knight(initialPosition.piece.Color);
                    var knightDir = new List<Direction>();
                    if (actualDir == Direction.North || actualDir == Direction.South)
                    {
                        knightDir.Add(Direction.East);
                        knightDir.Add(Direction.West);
                        var eastPos = CalculateNewPosition(board, newPosition, knightDir[0], 1);
                        if(eastPos != newPosition) knightPossibleMoves.Add(eastPos);
                        var westPos = CalculateNewPosition(board, newPosition, knightDir[1], 1);
                        if(westPos != newPosition) knightPossibleMoves.Add(westPos);
                        break;
                    }
                    knightDir.Add(Direction.South);
                    knightDir.Add(Direction.North);
                    var southPos = CalculateNewPosition(board, newPosition, knightDir[0], 1);
                    if(southPos != newPosition) knightPossibleMoves.Add(southPos);
                    var northPos = CalculateNewPosition(board, newPosition, knightDir[1], 1);
                    if(northPos != newPosition) knightPossibleMoves.Add(northPos);
                    break;
                }
                
                if (board.positions[newPosition.row, newPosition.column].piece?.Color == initialPosition.piece?.Color)
                    break;
                
                if (board.positions[newPosition.row, newPosition.column].piece != null && newPosition.piece?.Color != initialPosition.piece?.Color)
                {
                    if (newPosition.piece?.IsInCheckState == false && newPosition.piece?.Type != PieceEnum.King) previousEnemyPosition.Add(newPosition);
                }

                movesInSpecficDirection.Add(newPosition);
                possibleMoves.Add(newPosition);
                
                if (newPosition.piece?.Type != null && newPosition.piece?.Type == PieceEnum.King && newPosition.piece?.IsInCheckState == false && newPosition.piece?.Color != initialPosition.piece?.Color)
                {
                    var moves = new List<Position>();
                    var kingMove = VerifyKingMovementationCheck(board, newPosition);
                    var helperMove = PossiblePiecesHelpersToKingCheck(board, newPosition, movesInSpecficDirection);
                    moves.AddRange(kingMove);
                    if (helperMove.Count > 0) moves.AddRange(helperMove);
                    return (moves, newPosition.piece);
                }
                if(initialPosition.piece?.Type != PieceEnum.Knight && previousEnemyPosition.Any()) break;
            }
        }
        return initialPosition.piece?.Type == PieceEnum.Knight ? (knightPossibleMoves, initialPosition.piece) : (possibleMoves, initialPosition.piece);
    }
    
    private static Position CalculateNewPosition(Board board, Position initialPosition, Direction direction, int steps)
    {
        switch (direction)
        {
            case Direction.North:
            {
                if (!Common.IsInsideTheBoard(new Position(initialPosition.row, initialPosition.column - steps)))
                    return initialPosition;
                var newPosition = board.positions[initialPosition.row, initialPosition.column - steps];
                if (!Common.IsValidMove(board, newPosition, initialPosition)) return initialPosition;
                return newPosition;
            }

            case Direction.South:
            {
                if (!Common.IsInsideTheBoard(new Position(initialPosition.row, initialPosition.column + steps)))
                    return initialPosition;
                var newPosition = board.positions[initialPosition.row, initialPosition.column + steps];
                if (!Common.IsValidMove(board, newPosition, initialPosition)) return initialPosition;
                return newPosition;
            }

            case Direction.East:
            {
                if (!Common.IsInsideTheBoard(new Position(initialPosition.row + steps, initialPosition.column)))
                    return initialPosition;
                var newPosition = board.positions[initialPosition.row + steps, initialPosition.column];
                if (!Common.IsValidMove(board, newPosition, initialPosition)) return initialPosition;
                return newPosition;
            }

            case Direction.West:
            {
                if (!Common.IsInsideTheBoard(new Position(initialPosition.row - steps, initialPosition.column)))
                    return initialPosition;
                var newPosition = board.positions[initialPosition.row - steps, initialPosition.column];
                if (!Common.IsValidMove(board, newPosition, initialPosition)) return initialPosition;
                return newPosition;
            }

            case Direction.NorthEast:
            {         
                if (!Common.IsInsideTheBoard(new Position(initialPosition.row + steps, initialPosition.column - steps)))
                    return initialPosition;
                var newPosition = board.positions[initialPosition.row + steps, initialPosition.column - steps];
                if (!Common.IsValidMove(board, newPosition, initialPosition)) return initialPosition;
                return newPosition;
            }

            case Direction.SouthEast:
            {
                if (!Common.IsInsideTheBoard(new Position(initialPosition.row + steps, initialPosition.column + steps)))
                    return initialPosition;
                var newPosition = board.positions[initialPosition.row + steps, initialPosition.column + steps];
                if (!Common.IsValidMove(board, newPosition, initialPosition)) return initialPosition;
                return newPosition;
            }

            case Direction.NorthWest:
            {
                if (!Common.IsInsideTheBoard(new Position(initialPosition.row - steps, initialPosition.column - steps)))
                    return initialPosition;
                var newPosition = board.positions[initialPosition.row - steps, initialPosition.column - steps];
                if (!Common.IsValidMove(board, newPosition, initialPosition)) return initialPosition;
                return newPosition;
            }

            case Direction.SouthWest:
            {
                if (!Common.IsInsideTheBoard(new Position(initialPosition.row - steps, initialPosition.column + steps)))
                    return initialPosition;
                var newPosition = board.positions[initialPosition.row - steps, initialPosition.column + steps];
                if (!Common.IsValidMove(board, newPosition, initialPosition)) return initialPosition;
                return newPosition;
            }

            default:
                return initialPosition;
        }
    }

    private static List<Position> VerifyKingMovementationCheck(Board board, Position king)
    {
        king.piece!.IsInCheckState = true;
        
        var possibleMoveKing = king.piece?.GetPossibleMove(board, king);
        var possibleMoveOfOpponent = new List<Position>();

        var piecesOfOpponent = Common.GetOpponentPositions(board, king.piece!.Color);
        
        piecesOfOpponent.ForEach(x =>
        {
            var pos = x.piece!.GetPossibleMove(board, x);
            possibleMoveOfOpponent.AddRange(pos.possibleMoves);
        });

        foreach (var x in possibleMoveKing.Value.possibleMoves.ToArray())
        {
            if(possibleMoveOfOpponent.Contains(x)) possibleMoveKing.Value.possibleMoves.Remove(x);
        }

        return possibleMoveKing.Value.possibleMoves.Count == 0 ? null! : possibleMoveKing.Value.possibleMoves;
    }

    private static List<Position> PossiblePiecesHelpersToKingCheck(Board board, Position king, List<Position> possibleMovesInDirection)
    {
        var possibleFriendsMove = new List<Position>();
        var possibleValidFriendMoveToHelpKIng = new List<Position>();
        

        var piecesFriendFriends = Common.GetPieceByColorPositions(board, king.piece!.Color, PieceEnum.King);

        if (piecesFriendFriends.possibleMoves is null) return possibleValidFriendMoveToHelpKIng;
        foreach (var possibleFriendMove in piecesFriendFriends.possibleMoves)
        {
            possibleFriendsMove.AddRange(possibleFriendMove.piece!.GetPossibleMove(board, possibleFriendMove).possibleMoves);
        }

        possibleMovesInDirection.ForEach(x =>
        {
            if (possibleFriendsMove.Contains(x))
                possibleValidFriendMoveToHelpKIng.Add(x);
        });
        return possibleValidFriendMoveToHelpKIng;
    }

    public static async Task<bool> MakeMove(Board board, List<Position> possibleMoves, Position newPosition, Position oldPosition)
    {
        if (!possibleMoves.Contains(newPosition, new Common.PositionComparer())) return false;
        board.positions[newPosition.row, newPosition.column].piece = oldPosition.piece;
        board.positions[oldPosition.row, oldPosition.column].piece = null;
        var isInCheck = await IsKingInCheck(board, oldPosition.piece!.Color);
        if(isInCheck)
        {
            board.positions[newPosition.row, newPosition.column].piece = null;
            board.positions[oldPosition.row, oldPosition.column].piece = oldPosition.piece;
            return false;
        };
        return true;
    }

    public static async Task<bool> IsKingInCheck(Board board, ColorEnum color)
    {
        var piecesEnemy = Common.GetOpponentPositions(board, color);
        foreach (var position in piecesEnemy)
        {
            var pos = position.piece?.GetPossibleMove(board, position);
            if (pos.Value.actualPieceTrigger.Type == PieceEnum.King)
            {
                return true;
            }
        }
        return false;
    }
}