namespace Hibrygame;

public class Piece(ColorEnum color)
{
    public PieceEnum Type { get; set; } 
    public ColorEnum Color { get; set; } = color;
}