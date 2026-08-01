using Hibrygame.Enums;

namespace Hibrygame.Test.Hibrygame;

/// <summary>
/// Helpers para montar posicoes de teste em notacao algebraica e assertar
/// <em>quais</em> casas o motor devolve, em vez de apenas quantas.
///
/// A suite antiga chamava <c>StartBoard()</c> sem <c>MakePieceInInitialState()</c>,
/// o que produz um tabuleiro vazio de 64 casas. Isso esconde toda interacao entre
/// pecas. Use <see cref="NewGame"/> para o tabuleiro real de inicio de partida e
/// <see cref="Empty"/> quando quiser isolar uma peca de proposito.
/// </summary>
internal static class TestBoards
{
    /// <summary>Tabuleiro vazio (64 casas criadas, nenhuma peca).</summary>
    public static Board Empty()
    {
        var board = new Board();
        board.StartBoard();
        return board;
    }

    /// <summary>Tabuleiro na posicao inicial padrao de xadrez.</summary>
    public static Board NewGame()
    {
        var board = new Board();
        board.StartBoard();
        board.MakePieceInInitialState();
        return board;
    }

    /// <summary>Tabuleiro vazio com as pecas informadas, ex: <c>With(("e1", new King(White)))</c>.</summary>
    public static Board With(params (string square, Piece piece)[] pieces)
    {
        var board = Empty();
        foreach (var (square, piece) in pieces)
            board.Place(square, piece);
        return board;
    }

    public static Board Place(this Board board, string square, Piece? piece)
    {
        var (row, column) = Position.ToIndices(square);
        board.Positions[row, column].Piece = piece;
        return board;
    }

    public static Position At(this Board board, string square) => board.GetPositionByAlgebraic(square);

    public static Piece? PieceAt(this Board board, string square) => board.At(square).Piece;

    /// <summary>Casas de destino que o motor oferece para a peca em <paramref name="square"/>, ordenadas.</summary>
    public static string[] MovesFrom(this Board board, string square)
    {
        var from = board.At(square);
        var piece = from.Piece ?? throw new InvalidOperationException($"Nenhuma peca em '{square}'.");
        var (moves, _) = piece.GetPossibleMove(board, from);
        return (moves ?? new List<Position>())
            .Select(p => p.Algebraic)
            .Distinct()
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();
    }

    public static string[] Squares(params string[] squares) =>
        squares.OrderBy(s => s, StringComparer.Ordinal).ToArray();

    /// <summary>
    /// Impressao digital do tabuleiro: casa -> cor/tipo da peca. Usada para provar que
    /// gerar movimento e uma operacao de leitura e nao altera o estado da partida.
    /// </summary>
    public static Dictionary<string, string> Fingerprint(this Board board)
    {
        var snapshot = new Dictionary<string, string>();
        foreach (var position in board.Positions)
        {
            if (position is null) continue;
            snapshot[position.Algebraic] = position.Piece is null
                ? "-"
                : $"{position.Piece.Color}:{position.Piece.Type}";
        }
        return snapshot;
    }

    /// <summary>Todas as casas ocupadas por <paramref name="color"/>, ordenadas.</summary>
    public static string[] OccupiedBy(this Board board, ColorEnum color)
    {
        var squares = new List<string>();
        foreach (var position in board.Positions)
        {
            if (position?.Piece?.Color == color) squares.Add(position.Algebraic);
        }
        return squares.OrderBy(s => s, StringComparer.Ordinal).ToArray();
    }
}
