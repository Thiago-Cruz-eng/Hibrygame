using Hibrygame.Enums;

namespace Hibrygame;

public static class Common
{
    public static bool IsInsideTheBoard(Position newPosition)
    {
        return newPosition.Row is >= 0 and < 8 &&
                 newPosition.Column is >= 0 and < 8;
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
