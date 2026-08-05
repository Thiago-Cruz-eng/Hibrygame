using Hibrygame.Enums;

namespace Hibrygame;

/// <summary>
/// Rainha. Peça deslizante nas oito direções — retas e diagonais.
///
/// <para>
/// A geometria vive em <see cref="Move"/>, não aqui. Ver <see cref="Piece"/> para o porquê. Lá a
/// rainha e o rei compartilham a mesma lista de direções; a diferença é só o alcance, 8 contra 1.
/// </para>
/// </summary>
public class Queen : Piece
{
    public Queen(ColorEnum color)
    {
        Color = color;
        Type = PieceEnum.Queen;
    }
}
