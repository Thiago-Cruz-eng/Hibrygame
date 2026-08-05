using Hibrygame.Enums;

namespace Hibrygame;

/// <summary>
/// Torre. Peça deslizante: percorre as quatro direções retas até bater em algo.
///
/// <para>
/// A geometria vive em <see cref="Move"/>, não aqui. Ver <see cref="Piece"/> para o porquê.
/// </para>
///
/// <para>
/// Não há roque no jogo hoje. Quando houver, ele dependerá de
/// <see cref="Piece.HasAlreadyOneMove"/> desta peça e do rei.
/// </para>
/// </summary>
public class Rook : Piece
{
    public Rook(ColorEnum color)
    {
        Color = color;
        Type = PieceEnum.Rook;
    }
}
