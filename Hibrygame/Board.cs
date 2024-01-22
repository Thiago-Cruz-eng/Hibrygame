using Hibrygame.Enums;

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
        return pos;
    }

    public List<Position> GetPositionsPlaced()
    {
        var pos = new List<Position>();
        foreach (var position in positions)
        {
            if (position.piece?.Type != null)
                pos.Add(position);
        }
        return pos;
    }
}

