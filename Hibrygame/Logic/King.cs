using Hibrygame.Enums;

namespace Hibrygame;

/// <summary>
/// Rei. Anda uma casa em qualquer das oito direções.
///
/// <para>
/// A geometria vive em <see cref="Move"/>, não aqui. Ver <see cref="Piece"/> para o porquê.
/// </para>
///
/// <para>
/// O rei é a peça em torno da qual a legalidade gira: um lance só é legal se não deixar o
/// <b>próprio</b> rei em xeque, e é essa checagem que <c>Move.LegalMovesFor</c> aplica a todos os
/// candidatos. O estado de xeque fica em <see cref="Piece.IsInCheckState"/>, sincronizado por
/// <c>Move.IsKingInCheck</c> e exposto ao frontend no snapshot.
/// </para>
///
/// <para>
/// Não há roque no jogo hoje.
/// </para>
/// </summary>
public class King : Piece
{
    public King(ColorEnum color)
    {
        Color = color;
        Type = PieceEnum.King;
    }
}
