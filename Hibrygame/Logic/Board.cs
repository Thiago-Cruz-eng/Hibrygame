using Hibrygame.Enums;

namespace Hibrygame;

public class Board
{
    public Position[,] Positions { get; set; } = new Position[8, 8];

    public List<Position> StartBoard()
    {
        var pos = new List<Position>();
        for (var r = 0; r < 8; r++)
        {
            for (var c = 0; c < 8; c++)
            {
                Positions[r, c] = new Position(r, c);
                pos.Add(Positions[r, c]);
            }
        }
        return pos;
    }

    public void MakePieceInInitialState()
    {
        foreach (var position in Positions)
        {
            if (position is null) continue;
            position.SquareColor = (position.Row + position.Column) % 2 == 1 ? ColorEnum.Black : ColorEnum.White;
            position.Piece = DefinePiece(position);
        }
    }

    private static Piece? DefinePiece(Position pos)
    {
        if (pos.Column == 7)
        {
            return pos.Row switch
            {
                0 or 7 => new Rook(ColorEnum.White),
                1 or 6 => new Knight(ColorEnum.White),
                2 or 5 => new Bishop(ColorEnum.White),
                3 => new Queen(ColorEnum.White),
                4 => new King(ColorEnum.White),
                _ => null
            };
        }
        if (pos.Column == 6) return new Pawn(ColorEnum.White);
        if (pos.Column == 1) return new Pawn(ColorEnum.Black);
        if (pos.Column == 0)
        {
            return pos.Row switch
            {
                0 or 7 => new Rook(ColorEnum.Black),
                1 or 6 => new Knight(ColorEnum.Black),
                2 or 5 => new Bishop(ColorEnum.Black),
                3 => new Queen(ColorEnum.Black),
                4 => new King(ColorEnum.Black),
                _ => null
            };
        }
        return null;
    }

    public List<Position> GetPositionsPlaced()
    {
        var pos = new List<Position>();
        foreach (var position in Positions)
        {
            if (position?.Piece is null) continue;
            pos.Add(position);
        }
        return pos;
    }

    public List<Position> GetAllSquares()
    {
        var pos = new List<Position>();
        foreach (var position in Positions)
        {
            if (position is null) continue;
            pos.Add(position);
        }
        return pos;
    }

    public List<Position> GetPositionsPlacedInBoard() => GetPositionsPlaced();

    public Position GetPositionInBoard(int row, int column)
    {
        return Positions[row, column];
    }

    public Position GetPositionByAlgebraic(string notation)
    {
        var (row, column) = Position.ToIndices(notation);
        return Positions[row, column];
    }

    public async Task MakeMove(Board board, List<Position> possibleMoves, Position newPosition, Position oldPosition)
    {
        await Move.MakeMove(board, possibleMoves, newPosition, oldPosition);
    }
}
