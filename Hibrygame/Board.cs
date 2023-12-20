using System.Security.AccessControl;

namespace ClassLibrary1;

public class Board
{
    public Position[,] positions { get; set; }

    public List<Position> StartBoard()
    {
        var pos = new List<Position>();
        for (var r = 0; r < 8; r++)
        {
            for (var c = 0; c < 8; c++)
            {
                positions[r, c] = new Position(r, c);
                pos.Add(positions[r, c]);
            }
        }

        positions[0, 2].piece = new Hook();

        return pos;
    }
}

public class Position(int row, int column)
{
    public ColorEnum squareColor { get; set; }
    public Piece piece { get; set; }
    public int row { get; set; } = row;
    public int column { get; set; } = column;
}







































public enum PieceEnum
{
    Pawn,
    Bishop,
    Knight,
    Rook,
    Queen,
    King
}

public enum ColorEnum
{
    Black,
    White,
    None
}