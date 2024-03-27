using System.Data;
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
        var board = StartBoard();
        board.ForEach(pos =>
        {
            pos.SquareColor = (pos.Row + pos.Column) % 2 == 1 ? ColorEnum.Black : ColorEnum.White;
            pos.Piece = DefinePiece(pos);
        });
    }

    private static Piece? DefinePiece(Position pos)
    {
        Piece singlePiece = null;
        if (pos.Row == 0 && pos.Column == 7) singlePiece = new Rook(ColorEnum.White);
        if (pos.Row == 1 && pos.Column == 7) singlePiece = new Knight(ColorEnum.White);
        if (pos.Row == 2 && pos.Column == 7) singlePiece = new Bishop(ColorEnum.White);
        if (pos.Row == 3 && pos.Column == 7) singlePiece = new Queen(ColorEnum.White);
        if (pos.Row == 4 && pos.Column == 7) singlePiece = new King(ColorEnum.White);
        if (pos.Row == 5 && pos.Column == 7) singlePiece = new Bishop(ColorEnum.White);
        if (pos.Row == 6 && pos.Column == 7) singlePiece = new Knight(ColorEnum.White);
        if (pos.Row == 7 && pos.Column == 7) singlePiece = new Rook(ColorEnum.White);
        if (pos.Row == 0 && pos.Column == 6) singlePiece = new Pawn(ColorEnum.White);
        if (pos.Row == 1 && pos.Column == 6) singlePiece = new Pawn(ColorEnum.White);
        if (pos.Row == 2 && pos.Column == 6) singlePiece = new Pawn(ColorEnum.White);
        if (pos.Row == 3 && pos.Column == 6) singlePiece = new Pawn(ColorEnum.White);
        if (pos.Row == 4 && pos.Column == 6) singlePiece = new Pawn(ColorEnum.White);
        if (pos.Row == 5 && pos.Column == 6) singlePiece = new Pawn(ColorEnum.White);
        if (pos.Row == 6 && pos.Column == 6) singlePiece = new Pawn(ColorEnum.White);
        if (pos.Row == 7 && pos.Column == 6) singlePiece = new Pawn(ColorEnum.White);

        if (pos.Row == 0 && pos.Column == 0) singlePiece = new Rook(ColorEnum.Black);
        if (pos.Row == 1 && pos.Column == 0) singlePiece = new Knight(ColorEnum.Black);
        if (pos.Row == 2 && pos.Column == 0) singlePiece = new Bishop(ColorEnum.Black);
        if (pos.Row == 3 && pos.Column == 0) singlePiece = new Queen(ColorEnum.Black);
        if (pos.Row == 4 && pos.Column == 0) singlePiece = new King(ColorEnum.Black);
        if (pos.Row == 5 && pos.Column == 0) singlePiece = new Bishop(ColorEnum.Black);
        if (pos.Row == 6 && pos.Column == 0) singlePiece = new Knight(ColorEnum.Black);
        if (pos.Row == 7 && pos.Column == 0) singlePiece = new Rook(ColorEnum.Black);
        if (pos.Row == 0 && pos.Column == 1) singlePiece = new Pawn(ColorEnum.Black);
        if (pos.Row == 1 && pos.Column == 1) singlePiece = new Pawn(ColorEnum.Black);
        if (pos.Row == 2 && pos.Column == 1) singlePiece = new Pawn(ColorEnum.Black);
        if (pos.Row == 3 && pos.Column == 1) singlePiece = new Pawn(ColorEnum.Black);
        if (pos.Row == 4 && pos.Column == 1) singlePiece = new Pawn(ColorEnum.Black);
        if (pos.Row == 5 && pos.Column == 1) singlePiece = new Pawn(ColorEnum.Black);
        if (pos.Row == 6 && pos.Column == 1) singlePiece = new Pawn(ColorEnum.Black);
        if (pos.Row == 7 && pos.Column == 1) singlePiece = new Pawn(ColorEnum.Black);

        return singlePiece;
    }

    public List<Position> GetPositionsPlaced()
    {
        var pos = new List<Position>();
        foreach (var position in Positions)
        {
            pos.Add(position);
        }
        return pos;
    }
    
    public Position GetPositionInBoard(int row, int column)
    {
        return Positions[row, column];
    }
    
    public async Task MakeMove(Board board, List<Position> possibleMoves, Position newPosition, Position oldPosition)
    {
        await Move.MakeMove(board, possibleMoves, newPosition, oldPosition);
    }
}

