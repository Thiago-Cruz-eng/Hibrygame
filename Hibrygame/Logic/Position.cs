using Hibrygame.Enums;

namespace Hibrygame;

public class Position(int row, int column)
{
    public ColorEnum SquareColor { get; set; } = ColorEnum.None;
    public Piece? Piece { get; set; }
    public int Row { get; set; } = row;
    public int Column { get; set; } = column;
    public bool HighlightedPosition  { get; set; }

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
}
