using System.Net;

namespace Hibrygame;

public static class Move
{
    public static List<Position> CalculatePossibleMove(Board board, Position pos, List<Direction> dir, int squares)
    {
    

        
        var possibleMoves = new List<Position>();
        var initialState = board.GetPositionsPlaced();
        var initialPosition = initialState.FirstOrDefault(x => x.row == pos.row && x.column == pos.column);

        if (initialPosition == null) return possibleMoves;
        foreach (var actualDir in dir)
        {
            var previousEnemyPosition = new List<Position>();
            for (var i = 1; i <= squares; i++)
            {
                if (initialPosition.piece?.Type == PieceEnum.Knight && i == squares )
                {
                    var knightDir = new List<Direction>();
                    if (actualDir == Direction.North || actualDir == Direction.South)
                    {
                        knightDir.Add(Direction.East);
                        knightDir.Add(Direction.West);
                        possibleMoves.Add(CalculateNewPosition(board, initialPosition, knightDir[0], i));
                        possibleMoves.Add(CalculateNewPosition(board, initialPosition, knightDir[1], i));
                        return possibleMoves;
                    }
                    knightDir.Add(Direction.South);
                    knightDir.Add(Direction.North);
                    possibleMoves.Add(CalculateNewPosition(board, initialPosition, knightDir[0], i));
                    possibleMoves.Add(CalculateNewPosition(board, initialPosition, knightDir[1], i));
                    return possibleMoves;
                }
                
                var newPosition = CalculateNewPosition(board, initialPosition, actualDir, i);
                    
                if(initialPosition.piece?.Type == PieceEnum.Pawn && 
                   (actualDir == Direction.NorthEast ||
                   actualDir == Direction.NorthWest ||
                   actualDir == Direction.SouthEast ||
                   actualDir == Direction.SouthWest) &&
                   newPosition.piece?.Color == null)
                    break;
                
                if (board.positions[newPosition.row, newPosition.column].piece?.Color == initialPosition.piece?.Color)
                    break;
                
                if (board.positions[newPosition.row, newPosition.column].piece != null && newPosition.piece?.Color != initialPosition.piece?.Color)
                {
                    previousEnemyPosition.Add(newPosition);
                }

                var kingCheck = possibleMoves.First(x => x.piece?.Type == PieceEnum.King);
                if (kingCheck.piece!.Type == PieceEnum.King)
                {
                    var kingMove = VerifyKingMovimentationCheck(board, kingCheck);
                    var helperMove = PossibleHelpersToKingCheck(board,initialPosition, kingCheck);
                    possibleMoves.AddRange(kingMove);
                    possibleMoves.AddRange(helperMove);
                    return possibleMoves;
                };
                
                possibleMoves.Add(newPosition);
                if(previousEnemyPosition.Any()) break;
            }
        }
        return possibleMoves;
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

    private static List<Position> VerifyKingMovimentationCheck(Board board, Position king)
    {
        var possibleMoveWithoutTreat = new List<Position>();
        // se meu novo movimento tiver o rei inimigo como alvo então V
        
        // possiveis movimentos do rei em perigo
        var possibleMoveKing = king.piece?.GetPossibleMove(board, king);

        var piecesOfOpponent = Common.GetOpponentPositions(board, king.piece!.Color);
        var possibleMoveOfOpponent = new List<Position>();
        piecesOfOpponent.ForEach(x =>
        {
            var pos = x.piece!.GetPossibleMove(board, x);
            possibleMoveOfOpponent.AddRange(pos);
        });
        
        possibleMoveOfOpponent.ForEach(x =>
        {
            // verificar se nos possiveis novos movimentos das peças inimigas tem possibilidade de capturar o meu rei
            if(!possibleMoveKing.Contains(x)) possibleMoveWithoutTreat.Add(x);
        });
        
        // se nenhuma peça aliada puder capturar o rei em suas novas possiveis posições ou alguma validação acima for verdaira
        // entao, somente check. caso 
        // meu inimigo nao tenha peças para colocar na frente do meu movimento nem peças para capturar minha peça trigger 
        // então check mate
        return possibleMoveWithoutTreat;
    }

    private static List<Position> PossibleHelpersToKingCheck(Board board, Position enemyTrigger, Position king)
    {
        // e meu inimigo tenha peças para colocar na frente da rota da minha peça trigger
        // e se existem peças inimigas que podem capturar minha peça trigger que armou o check
        var possibleFriendsMove = new List<Position>();
        var possibleMoveEnemyTrigger = enemyTrigger.piece!.GetPossibleMove(board, enemyTrigger);

        var possibleMoveFriends = Common.GetByColorPositions(board, king.piece!.Color);

        possibleMoveEnemyTrigger.ForEach(x =>
        {
            if (possibleMoveFriends.Contains(x))
                possibleFriendsMove.Add(x);
            
        });
        return possibleFriendsMove;
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