using Hibrygame.Enums;

namespace Hibrygame;

/// <summary>
/// Uma casa do tabuleiro.
///
/// Coordenadas duplas: <see cref="Row"/>/<see cref="Column"/> sao os indices internos,
/// <see cref="Algebraic"/> e a notacao que atravessa a API. O mapeamento e
/// <c>file = 'a' + Row</c> e <c>rank = 8 - Column</c>, ou seja Column 7 e a primeira
/// fileira (brancas) e Column 0 e a oitava (pretas).
///
/// A identidade de uma casa e o par linha/coluna, e nada mais: duas Position com as
/// mesmas coordenadas sao a mesma casa, independentemente da peca que esteja nela. Por
/// isso Row e Column sao somente leitura — se mudassem, o hash mudaria com elas.
/// </summary>
public class Position(int row, int column) : IEquatable<Position>
{
    public ColorEnum SquareColor { get; set; } = ColorEnum.None;
    public Piece? Piece { get; set; }
    public int Row { get; } = row;
    public int Column { get; } = column;
    public bool HighlightedPosition { get; set; }

    public char File => (char)('a' + Row);
    public int Rank => 8 - Column;
    public string Algebraic => $"{File}{Rank}";

    public static Position FromAlgebraic(string notation)
    {
        if (!TryFromAlgebraic(notation, out var pos))
            throw new ArgumentException($"Invalid algebraic notation: '{notation}'.", nameof(notation));
        return pos!;
    }

    public static bool TryFromAlgebraic(string notation, out Position? position)
    {
        position = null;
        if (string.IsNullOrWhiteSpace(notation) || notation.Length != 2)
            return false;

        var fileChar = char.ToLowerInvariant(notation[0]);
        if (fileChar < 'a' || fileChar > 'h')
            return false;

        if (!int.TryParse(notation[1].ToString(), out var rank) || rank < 1 || rank > 8)
            return false;

        var row = fileChar - 'a';
        var column = 8 - rank;
        position = new Position(row, column);
        return true;
    }

    public static (int row, int column) ToIndices(string notation)
    {
        var pos = FromAlgebraic(notation);
        return (pos.Row, pos.Column);
    }

    public bool IsInsideBoard() => Row is >= 0 and < 8 && Column is >= 0 and < 8;

    // Igualdade de valor (era a DT-12). Sem ela, Contains/Remove numa lista de Position
    // caiam em igualdade de referencia, e o motor precisava carregar um
    // EqualityComparer proprio para comparar casas — que era passado em alguns pontos e
    // esquecido em outros.
    public bool Equals(Position? other) =>
        other is not null && other.Row == Row && other.Column == Column;

    public override bool Equals(object? obj) => Equals(obj as Position);

    public override int GetHashCode() => HashCode.Combine(Row, Column);

    public override string ToString() => Algebraic;
}
