using System.Net;

namespace Hibrygame;

public static class Common
{
    public static bool IsInsideTheBoard(Position newPosition)
    {
        return newPosition.row is >= 0 and < 8 &&
                 newPosition.column is >= 0 and < 8;
    }
    
    public static bool IsValidMove(Board board, Position newPosition, Position initialPosition)
    {
        if (!IsInsideTheBoard(newPosition)) return false;
        
        if (board.positions[newPosition.row, newPosition.column].piece?.Type == null) return true;
        return board.positions[newPosition.row, newPosition.column].piece?.Color != initialPosition.piece?.Color;
    }

    public static List<Position> GetOpponentPositions(Board board, ColorEnum opponent)
    {
        var pos = new List<Position>();
        _ = opponent == ColorEnum.Black ? opponent = ColorEnum.White : opponent = ColorEnum.Black;
        
        foreach (var position in board.positions)
        {
            if (position.piece?.Color == opponent)
                pos.Add(position);
        }

        return pos;
    }
    public static (List<Position> possibleMoves, Piece? actualPieceTrigger) GetPieceByColorPositions(Board board, ColorEnum color, PieceEnum excludePiece)
    {
        var pos = new List<Position>();
        var positionWithOutExcludePiece = new List<Position>();

        foreach (var position in board.positions)
        {
            if (position.piece?.Color != color) continue;
            pos.Add(position);
            positionWithOutExcludePiece.Add(position);
        }
        
        foreach (var position in pos)
        {
            if(position.piece!.Type == excludePiece)
                positionWithOutExcludePiece.Remove(position);
            return (positionWithOutExcludePiece, position.piece);
        }

        return (null, null);
    }
    
}