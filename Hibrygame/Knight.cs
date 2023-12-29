﻿namespace Hibrygame;

public class Knight : Piece
{
    public Knight(ColorEnum color)
    {
        Color = color;
        Type = PieceEnum.Knight;
    }
    
    public override List<Position> GetPossibleMove(Board board, Position pos)
    {
        var direction = new List<Direction>
        {
            Direction.North,
            Direction.South,
            Direction.East,
            Direction.West,
        };
        
        return Move.CalculatePossibleMove(board, pos, direction, 3);
    }
}