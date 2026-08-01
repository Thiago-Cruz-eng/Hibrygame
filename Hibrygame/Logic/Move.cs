using Hibrygame.Enums;

namespace Hibrygame;

/// <summary>
/// Geracao e aplicacao de movimento.
///
/// Organizado em tres camadas, de baixo para cima:
///
/// 1. <see cref="AttackedSquares"/> — geometria pura. Onde a peca alcanca, ignorando
///    se o lance e legal. E a unica camada que a deteccao de xeque consulta, o que
///    evita a recursao infinita entre "que lances tenho" e "estou em xeque".
/// 2. <see cref="CandidateMoves"/> — geometria mais as regras de ocupacao da casa de
///    destino (peao nao captura para frente, ninguem captura peca da propria cor).
/// 3. <see cref="LegalMoves"/> — candidatos menos os que deixariam o proprio rei em
///    xeque. E o que <see cref="CalculatePossibleMove"/> devolve.
///
/// Nenhuma dessas camadas altera a posicao das pecas: simular um lance para testar
/// legalidade sempre desfaz a simulacao.
/// </summary>
public static class Move
{
    private static readonly (int Row, int Column)[] KnightOffsets =
    {
        (1, 2), (2, 1), (2, -1), (1, -2), (-1, -2), (-2, -1), (-2, 1), (-1, 2)
    };

    private static readonly Direction[] AllDirections =
    {
        Direction.North, Direction.South, Direction.East, Direction.West,
        Direction.NorthEast, Direction.SouthEast, Direction.NorthWest, Direction.SouthWest
    };

    // -----------------------------------------------------------------
    // API publica
    // -----------------------------------------------------------------

    /// <summary>
    /// Lances legais da peca em <paramref name="pos"/>. <paramref name="dir"/> e
    /// <paramref name="squares"/> descrevem o alcance da peca (ver as classes de peca);
    /// peao e cavalo tem geometria propria e ignoram <paramref name="dir"/>.
    /// </summary>
    public static (List<Position> possibleMoves, Piece? actualPieceTrigger) CalculatePossibleMove(
        Board board, Position pos, List<Direction> dir, int squares)
    {
        var origin = Resolve(board, pos);
        if (origin?.Piece is null) return (new List<Position>(), null);

        var legal = LegalMoves(board, origin, dir, squares);
        legal.ForEach(square => square.HighlightedPosition = true);
        return (legal, origin.Piece);
    }

    /// <summary>
    /// Aplica <paramref name="oldPosition"/> -> <paramref name="newPosition"/> se for um
    /// dos <paramref name="possibleMoves"/>. Devolve false e deixa o tabuleiro intacto
    /// quando o lance nao e permitido.
    /// </summary>
    public static Task<bool> MakeMove(Board board, List<Position> possibleMoves, Position newPosition, Position oldPosition)
    {
        if (possibleMoves is null || !possibleMoves.Contains(newPosition, new Common.PositionComparer()))
            return Task.FromResult(false);

        var source = Resolve(board, oldPosition);
        var target = Resolve(board, newPosition);
        if (source?.Piece is null || target is null) return Task.FromResult(false);

        // A cor tem de ser lida antes de mexer no tabuleiro: `source` e a propria casa
        // do tabuleiro, e limpa-la apaga a peca que estamos a consultar. Ler depois era
        // o motivo pelo qual a verificacao de auto-xeque nunca corria.
        var moving = source.Piece;
        var movingColor = moving.Color;

        var captured = target.Piece;
        target.Piece = moving;
        source.Piece = null;

        if (IsInCheck(board, movingColor))
        {
            source.Piece = moving;
            target.Piece = captured;
            return Task.FromResult(false);
        }

        moving.HasAlreadyOneMove = true;

        board.GetAllSquares().ForEach(square => square.HighlightedPosition = false);
        RefreshCheckFlags(board);

        return Task.FromResult(true);
    }

    /// <summary>
    /// O rei de <paramref name="color"/> esta em xeque? Tambem sincroniza
    /// <see cref="Piece.IsInCheckState"/> desse rei, que o snapshot enviado ao
    /// frontend expoe.
    /// </summary>
    public static Task<bool> IsKingInCheck(Board board, ColorEnum? color)
    {
        if (color is null || color == ColorEnum.None) return Task.FromResult(false);

        var inCheck = IsInCheck(board, color.Value);

        var king = FindKing(board, color.Value);
        if (king?.Piece is not null) king.Piece.IsInCheckState = inCheck;

        return Task.FromResult(inCheck);
    }

    /// <summary>A casa e atacada por alguma peca de <paramref name="byColor"/>?</summary>
    public static bool IsSquareAttacked(Board board, int row, int column, ColorEnum byColor)
    {
        foreach (var position in board.Positions)
        {
            if (position?.Piece is null || position.Piece.Color != byColor) continue;

            foreach (var (attackedRow, attackedColumn) in AttackedSquares(board, position))
            {
                if (attackedRow == row && attackedColumn == column) return true;
            }
        }
        return false;
    }

    // -----------------------------------------------------------------
    // Camada 3 — legalidade
    // -----------------------------------------------------------------

    private static List<Position> LegalMoves(Board board, Position origin, List<Direction>? dir, int squares)
    {
        var piece = origin.Piece!;
        var legal = new List<Position>();

        foreach (var target in CandidateMoves(board, origin, dir, squares))
        {
            var captured = target.Piece;
            target.Piece = piece;
            origin.Piece = null;

            var exposesOwnKing = IsInCheck(board, piece.Color);

            origin.Piece = piece;
            target.Piece = captured;

            if (!exposesOwnKing) legal.Add(target);
        }

        return legal;
    }

    private static bool IsInCheck(Board board, ColorEnum color)
    {
        var king = FindKing(board, color);
        if (king is null) return false;

        var opponent = color == ColorEnum.Black ? ColorEnum.White : ColorEnum.Black;
        return IsSquareAttacked(board, king.Row, king.Column, opponent);
    }

    private static void RefreshCheckFlags(Board board)
    {
        foreach (var color in new[] { ColorEnum.White, ColorEnum.Black })
        {
            var king = FindKing(board, color);
            if (king?.Piece is not null) king.Piece.IsInCheckState = IsInCheck(board, color);
        }
    }

    // -----------------------------------------------------------------
    // Camada 2 — candidatos (geometria + ocupacao do destino)
    // -----------------------------------------------------------------

    private static List<Position> CandidateMoves(Board board, Position origin, List<Direction>? dir, int squares)
    {
        var piece = origin.Piece!;

        return piece.Type switch
        {
            PieceEnum.Pawn => PawnCandidates(board, origin, squares),
            PieceEnum.Knight => KnightCandidates(board, origin),
            _ => SlidingCandidates(board, origin, dir ?? AllDirections.ToList(), squares)
        };
    }

    private static List<Position> SlidingCandidates(Board board, Position origin, List<Direction> dir, int squares)
    {
        var moves = new List<Position>();
        var color = origin.Piece!.Color;

        foreach (var direction in dir)
        {
            var (dRow, dColumn) = Delta(direction);

            for (var step = 1; step <= squares; step++)
            {
                var row = origin.Row + dRow * step;
                var column = origin.Column + dColumn * step;
                if (!IsInsideBoard(row, column)) break;

                var target = board.Positions[row, column];
                if (target.Piece is null)
                {
                    moves.Add(target);
                    continue;
                }

                // A primeira peca no caminho encerra a direcao. Se for inimiga, a casa
                // dela e capturavel; se for da propria cor, nao.
                if (target.Piece.Color != color) moves.Add(target);
                break;
            }
        }

        return moves;
    }

    private static List<Position> KnightCandidates(Board board, Position origin)
    {
        var moves = new List<Position>();
        var color = origin.Piece!.Color;

        foreach (var (dRow, dColumn) in KnightOffsets)
        {
            var row = origin.Row + dRow;
            var column = origin.Column + dColumn;
            if (!IsInsideBoard(row, column)) continue;

            // O cavalo salta: o que estiver entre origem e destino e irrelevante.
            var target = board.Positions[row, column];
            if (target.Piece?.Color == color) continue;

            moves.Add(target);
        }

        return moves;
    }

    private static List<Position> PawnCandidates(Board board, Position origin, int squares)
    {
        var moves = new List<Position>();
        var pawn = origin.Piece!;
        var (dRow, dColumn) = Delta(pawn.Color == ColorEnum.Black ? Direction.South : Direction.North);

        // Avanco: nunca captura, e nunca salta sobre uma peca.
        for (var step = 1; step <= Math.Max(1, squares); step++)
        {
            var row = origin.Row + dRow * step;
            var column = origin.Column + dColumn * step;
            if (!IsInsideBoard(row, column)) break;

            var ahead = board.Positions[row, column];
            if (ahead.Piece is not null) break;

            moves.Add(ahead);
        }

        // Captura: apenas na diagonal, e apenas se houver peca inimiga la.
        foreach (var (captureRow, captureColumn) in PawnCaptureSquares(origin, pawn.Color))
        {
            var target = board.Positions[captureRow, captureColumn];
            if (target.Piece is not null && target.Piece.Color != pawn.Color) moves.Add(target);
        }

        return moves;
    }

    // -----------------------------------------------------------------
    // Camada 1 — geometria pura, usada pela deteccao de xeque
    // -----------------------------------------------------------------

    /// <summary>
    /// Casas que a peca em <paramref name="origin"/> ataca. Difere dos candidatos em
    /// dois pontos: o peao ataca as diagonais mesmo que estejam vazias e nao ataca a
    /// casa a frente, e uma casa ocupada por peca amiga continua defendida (por isso o
    /// rei inimigo nao pode captura-la).
    /// </summary>
    private static IEnumerable<(int Row, int Column)> AttackedSquares(Board board, Position origin)
    {
        var piece = origin.Piece!;

        switch (piece.Type)
        {
            case PieceEnum.Pawn:
                foreach (var square in PawnCaptureSquares(origin, piece.Color)) yield return square;
                yield break;

            case PieceEnum.Knight:
                foreach (var (dRow, dColumn) in KnightOffsets)
                {
                    var row = origin.Row + dRow;
                    var column = origin.Column + dColumn;
                    if (IsInsideBoard(row, column)) yield return (row, column);
                }
                yield break;

            default:
                var (directions, range) = SlidingRange(piece.Type);
                foreach (var direction in directions)
                {
                    var (dRow, dColumn) = Delta(direction);
                    for (var step = 1; step <= range; step++)
                    {
                        var row = origin.Row + dRow * step;
                        var column = origin.Column + dColumn * step;
                        if (!IsInsideBoard(row, column)) break;

                        yield return (row, column);

                        // A peca bloqueadora e atacada, mas o raio nao a atravessa.
                        if (board.Positions[row, column].Piece is not null) break;
                    }
                }
                yield break;
        }
    }

    private static IEnumerable<(int Row, int Column)> PawnCaptureSquares(Position origin, ColorEnum color)
    {
        var diagonals = color == ColorEnum.Black
            ? new[] { Direction.SouthEast, Direction.SouthWest }
            : new[] { Direction.NorthEast, Direction.NorthWest };

        foreach (var direction in diagonals)
        {
            var (dRow, dColumn) = Delta(direction);
            var row = origin.Row + dRow;
            var column = origin.Column + dColumn;
            if (IsInsideBoard(row, column)) yield return (row, column);
        }
    }

    private static (Direction[] Directions, int Range) SlidingRange(PieceEnum type) => type switch
    {
        PieceEnum.Rook => (new[] { Direction.North, Direction.South, Direction.East, Direction.West }, 8),
        PieceEnum.Bishop => (new[] { Direction.NorthEast, Direction.SouthEast, Direction.NorthWest, Direction.SouthWest }, 8),
        PieceEnum.Queen => (AllDirections, 8),
        PieceEnum.King => (AllDirections, 1),
        _ => (Array.Empty<Direction>(), 0)
    };

    // -----------------------------------------------------------------
    // Utilitarios
    // -----------------------------------------------------------------

    private static (int Row, int Column) Delta(Direction direction) => direction switch
    {
        Direction.North => (0, -1),
        Direction.South => (0, 1),
        Direction.East => (1, 0),
        Direction.West => (-1, 0),
        Direction.NorthEast => (1, -1),
        Direction.NorthWest => (-1, -1),
        Direction.SouthEast => (1, 1),
        Direction.SouthWest => (-1, 1),
        _ => (0, 0)
    };

    private static bool IsInsideBoard(int row, int column) =>
        row is >= 0 and < 8 && column is >= 0 and < 8;

    /// <summary>
    /// Devolve a casa do tabuleiro correspondente a <paramref name="pos"/>. Aceita uma
    /// <see cref="Position"/> solta (so linha/coluna) porque a suite historica chama a
    /// geracao com posicoes construidas a mao.
    /// </summary>
    private static Position? Resolve(Board board, Position? pos)
    {
        if (pos is null || !IsInsideBoard(pos.Row, pos.Column)) return null;
        return board.Positions[pos.Row, pos.Column];
    }

    private static Position? FindKing(Board board, ColorEnum color)
    {
        foreach (var position in board.Positions)
        {
            if (position?.Piece?.Type == PieceEnum.King && position.Piece.Color == color) return position;
        }
        return null;
    }
}
