using Hibrygame.Enums;

namespace Hibrygame;

/// <summary>
/// Cavalo.
///
/// <para>
/// A geometria — os oito saltos em L, e o fato de saltar sobre peças — vive em <see cref="Move"/>,
/// não aqui. Ver <see cref="Piece"/> para o porquê.
/// </para>
/// </summary>
public class Knight : Piece
{
    public Knight(ColorEnum color)
    {
        Color = color;
        Type = PieceEnum.Knight;
    }
}
