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

    public static List<Position> GetOpponentPositions(Board board, ColorEnum color)
    {
        var opponent = color == ColorEnum.Black ? ColorEnum.White : ColorEnum.Black;
        var pos = new List<Position>();
        foreach (var position in board.Positions)
        {
            if (position?.Piece?.Color == opponent)
                pos.Add(position);
        }
        return pos;
    }

    public static (List<Position> possibleMoves, Piece? actualPieceTrigger) GetPieceByColorPositions(Board board, ColorEnum color, PieceEnum excludePiece)
    {
        var friends = new List<Position>();
        Piece? trigger = null;
        foreach (var position in board.Positions)
        {
            if (position?.Piece?.Color != color) continue;
            if (position.Piece.Type == excludePiece)
            {
                trigger = position.Piece;
                continue;
            }
            friends.Add(position);
        }
        return (friends, trigger);
    }

    public class PositionComparer : EqualityComparer<Position>
    {
        public override bool Equals(Position? x, Position? y)
        {
            if (x is null || y is null) return ReferenceEquals(x, y);
            return x.Column == y.Column && x.Row == y.Row;
        }

        public override int GetHashCode(Position obj) => HashCode.Combine(obj.Row, obj.Column);
    }
}
