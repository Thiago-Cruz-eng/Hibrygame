﻿using Hibrygame.Enums;

namespace Hibrygame;

public class King : Piece
{
    public King(ColorEnum color)
    {
        Color = color;
        Type = PieceEnum.King;
    }
    public override (List<Position> possibleMoves, Piece? actualPieceTrigger) GetPossibleMove(Board board, Position pos)
    {
        var direction = new List<Direction>
        {
            Direction.North,
            Direction.South,
            Direction.East,
            Direction.West,
            Direction.SouthEast,
            Direction.SouthWest,
            Direction.NorthEast,
            Direction.NorthWest,
        };
        
        return Move.CalculatePossibleMove(board, pos, direction, 1);
    }
}