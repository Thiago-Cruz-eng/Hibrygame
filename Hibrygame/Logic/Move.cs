using Hibrygame.Enums;

namespace Hibrygame;

public static class Move
{
    public static (List<Position> possibleMoves, Piece? actualPieceTrigger) CalculatePossibleMove(Board board, Position pos, List<Direction> dir, int squares)
    {
        var possibleMoves = new List<Position>();
        var knightPossibleMoves = new List<Position>();
        var initialState = board.GetPositionsPlaced();
        var initialPosition = initialState.FirstOrDefault(x => x.Row == pos.Row && x.Column == pos.Column);
        var isInCheckState = false;

        if (initialPosition == null) return (possibleMoves, null);
        foreach (var actualDir in dir)
        {
            var movesInSpecficDirection = new List<Position>();
            var previousEnemyPosition = new List<Position>();
            for (var i = 1; i <= squares; i++)
            {
                var newPosition = CalculateNewPosition(board, initialPosition, actualDir, i);
                    
                if(initialPosition.Piece?.Type == PieceEnum.Pawn && 
                   (actualDir == Direction.NorthEast ||
                   actualDir == Direction.NorthWest ||
                   actualDir == Direction.SouthEast ||
                   actualDir == Direction.SouthWest) &&
                   newPosition.Piece?.Color == null)
                    break;
                
                if (initialPosition.Piece?.Type == PieceEnum.Knight && i == squares)
                {
                    newPosition.Piece = new Knight(initialPosition.Piece.Color);
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
                
                if (board.Positions[newPosition.Row, newPosition.Column].Piece?.Color == initialPosition.Piece?.Color)
                    break;
                
                if (board.Positions[newPosition.Row, newPosition.Column].Piece != null && newPosition.Piece?.Color != initialPosition.Piece?.Color)
                {
                    if (newPosition.Piece?.IsInCheckState == false && newPosition.Piece?.Type != PieceEnum.King) previousEnemyPosition.Add(newPosition);
                }

                movesInSpecficDirection.Add(newPosition);
                possibleMoves.Add(newPosition);
                
                if (newPosition.Piece?.Type != null && newPosition.Piece?.Type == PieceEnum.King && newPosition.Piece?.IsInCheckState == false && newPosition.Piece?.Color != initialPosition.Piece?.Color)
                {
                    var moves = new List<Position>();
                    var kingMove = VerifyKingMovementationCheck(board, newPosition);
                    var helperMove = PossiblePiecesHelpersToKingCheck(board, newPosition, movesInSpecficDirection);
                    moves.AddRange(kingMove);
                    if (helperMove.Count > 0) moves.AddRange(helperMove);
                    return (moves, newPosition.Piece);
                }
                if(initialPosition.Piece?.Type != PieceEnum.Knight && previousEnemyPosition.Any()) break;
            }
        }
        return initialPosition.Piece?.Type == PieceEnum.Knight ? (knightPossibleMoves, piece: initialPosition.Piece) : (possibleMoves, piece: initialPosition.Piece);
    }
    
    private static Position CalculateNewPosition(Board board, Position initialPosition, Direction direction, int steps)
    {
        switch (direction)
        {
            case Direction.North:
            {
                if (!Common.IsInsideTheBoard(new Position(initialPosition.Row, initialPosition.Column - steps)))
                    return initialPosition;
                var newPosition = board.Positions[initialPosition.Row, initialPosition.Column - steps];
                if (!Common.IsValidMove(board, newPosition, initialPosition)) return initialPosition;
                return newPosition;
            }

            case Direction.South:
            {
                if (!Common.IsInsideTheBoard(new Position(initialPosition.Row, initialPosition.Column + steps)))
                    return initialPosition;
                var newPosition = board.Positions[initialPosition.Row, initialPosition.Column + steps];
                if (!Common.IsValidMove(board, newPosition, initialPosition)) return initialPosition;
                return newPosition;
            }

            case Direction.East:
            {
                if (!Common.IsInsideTheBoard(new Position(initialPosition.Row + steps, initialPosition.Column)))
                    return initialPosition;
                var newPosition = board.Positions[initialPosition.Row + steps, initialPosition.Column];
                if (!Common.IsValidMove(board, newPosition, initialPosition)) return initialPosition;
                return newPosition;
            }

            case Direction.West:
            {
                if (!Common.IsInsideTheBoard(new Position(initialPosition.Row - steps, initialPosition.Column)))
                    return initialPosition;
                var newPosition = board.Positions[initialPosition.Row - steps, initialPosition.Column];
                if (!Common.IsValidMove(board, newPosition, initialPosition)) return initialPosition;
                return newPosition;
            }

            case Direction.NorthEast:
            {         
                if (!Common.IsInsideTheBoard(new Position(initialPosition.Row + steps, initialPosition.Column - steps)))
                    return initialPosition;
                var newPosition = board.Positions[initialPosition.Row + steps, initialPosition.Column - steps];
                if (!Common.IsValidMove(board, newPosition, initialPosition)) return initialPosition;
                return newPosition;
            }

            case Direction.SouthEast:
            {
                if (!Common.IsInsideTheBoard(new Position(initialPosition.Row + steps, initialPosition.Column + steps)))
                    return initialPosition;
                var newPosition = board.Positions[initialPosition.Row + steps, initialPosition.Column + steps];
                if (!Common.IsValidMove(board, newPosition, initialPosition)) return initialPosition;
                return newPosition;
            }

            case Direction.NorthWest:
            {
                if (!Common.IsInsideTheBoard(new Position(initialPosition.Row - steps, initialPosition.Column - steps)))
                    return initialPosition;
                var newPosition = board.Positions[initialPosition.Row - steps, initialPosition.Column - steps];
                if (!Common.IsValidMove(board, newPosition, initialPosition)) return initialPosition;
                return newPosition;
            }

            case Direction.SouthWest:
            {
                if (!Common.IsInsideTheBoard(new Position(initialPosition.Row - steps, initialPosition.Column + steps)))
                    return initialPosition;
                var newPosition = board.Positions[initialPosition.Row - steps, initialPosition.Column + steps];
                if (!Common.IsValidMove(board, newPosition, initialPosition)) return initialPosition;
                return newPosition;
            }

            default:
                return initialPosition;
        }
    }

    private static List<Position> VerifyKingMovementationCheck(Board board, Position king)
    {
        king.Piece!.IsInCheckState = true;
        
        var possibleMoveKing = king.Piece?.GetPossibleMove(board, king);
        var possibleMoveOfOpponent = new List<Position>();

        var piecesOfOpponent = Common.GetOpponentPositions(board, king.Piece!.Color);
        
        piecesOfOpponent.ForEach(x =>
        {
            var pos = x.Piece!.GetPossibleMove(board, x);
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
        

        var piecesFriendFriends = Common.GetPieceByColorPositions(board, king.Piece!.Color, PieceEnum.King);

        if (piecesFriendFriends.possibleMoves is null) return possibleValidFriendMoveToHelpKIng;
        foreach (var possibleFriendMove in piecesFriendFriends.possibleMoves)
        {
            possibleFriendsMove.AddRange(possibleFriendMove.Piece!.GetPossibleMove(board, possibleFriendMove).possibleMoves);
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
        board.Positions[newPosition.Row, newPosition.Column].Piece = oldPosition.Piece;
        board.Positions[oldPosition.Row, oldPosition.Column].Piece = null;
        var isInCheck = await IsKingInCheck(board, oldPosition.Piece!.Color);
        if(isInCheck)
        {
            board.Positions[newPosition.Row, newPosition.Column].Piece = null;
            board.Positions[oldPosition.Row, oldPosition.Column].Piece = oldPosition.Piece;
            return false;
        };
        return true;
    }

    public static async Task<bool> IsKingInCheck(Board board, ColorEnum color)
    {
        var piecesEnemy = Common.GetOpponentPositions(board, color);
        foreach (var position in piecesEnemy)
        {
            var pos = position.Piece?.GetPossibleMove(board, position);
            if (pos.Value.actualPieceTrigger.Type == PieceEnum.King)
            {
                return true;
            }
        }
        return false;
    }
}