using Hibrygame.Enums;

namespace Hibrygame;

/// <summary>
/// Bispo. Peça deslizante: percorre as quatro diagonais até bater em algo.
///
/// <para>
/// A geometria vive em <see cref="Move"/>, não aqui. Ver <see cref="Piece"/> para o porquê.
/// </para>
/// </summary>
public class Bishop : Piece
{
    public Bishop(ColorEnum color)
    {
        Color = color;
        Type = PieceEnum.Bishop;
    }
}
