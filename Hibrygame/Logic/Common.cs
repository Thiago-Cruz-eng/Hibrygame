using System.Net;
using Hibrygame.Enums;

namespace Hibrygame;

public static class Common
{
    public static bool IsInsideTheBoard(Position newPosition)
    {
        return newPosition.Row is >= 0 and < 8 &&
                 newPosition.Column is >= 0 and < 8;
    }
    
    public static bool IsValidMove(Board board, Position newPosition, Position initialPosition)
    {
        if (!IsInsideTheBoard(newPosition)) return false;
        
        if (board.Positions[newPosition.Row, newPosition.Column].Piece?.Type == null) return true;
        return board.Positions[newPosition.Row, newPosition.Column].Piece?.Color != initialPosition.Piece?.Color;
    }

    public static List<Position> GetOpponentPositions(Board board, ColorEnum opponent)
    {
        var pos = new List<Position>();
        _ = opponent == ColorEnum.Black ? opponent = ColorEnum.White : opponent = ColorEnum.Black;
        
        foreach (var position in board.Positions)
        {
            if (position.Piece?.Color == opponent)
                pos.Add(position);
        }

        return pos;
    }
    
    public static (List<Position> possibleMoves, Piece? actualPieceTrigger) GetPieceByColorPositions(Board board, ColorEnum color, PieceEnum excludePiece)
    {
        var pos = new List<Position>();
        var positionWithOutExcludePiece = new List<Position>();

        foreach (var position in board.Positions)
        {
            if (position.Piece?.Color != color) continue;
            pos.Add(position);
            positionWithOutExcludePiece.Add(position);
        }
        
        foreach (var position in pos)
        {
            if(position.Piece!.Type == excludePiece)
                positionWithOutExcludePiece.Remove(position);
            return (positionWithOutExcludePiece, position.Piece);
        }

        return (null, null);
    }
    
    public class PositionComparer : EqualityComparer<Position>
    {
        public override bool Equals(Position? x, Position? y)
        {
            return x.Column == y.Column && x.Row == y.Row;
        }

        public override int GetHashCode(Position obj)
        {
            return obj.GetHashCode();
        }
    }
}