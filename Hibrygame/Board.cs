namespace Hibrygame;

public class Board
{
    public Position[,] positions { get; set; } = new Position[8, 8];

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

        positions[0, 2].piece = new Rook(ColorEnum.Black);
        positions[5, 5].piece = new King(ColorEnum.White);
        return pos;
    }

    public List<Position> GetPositionsPlaced()
    {
        var pos = new List<Position>();
        foreach (var position in positions)
        {
            if (position.piece?.Type != null)
            {
                pos.Add(position);
            }        
        }

        return pos;
    }
}

public class Position(int row, int column)
{
    public ColorEnum squareColor { get; set; } = ColorEnum.None;
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
    King,
    None
}

public enum ColorEnum
{
    Black,
    White,
    None
}