using Hibrygame.Enums;

namespace Hibrygame;

/// <summary>
/// Peão.
///
/// <para>
/// <b>Não procure aqui como o peão se move.</b> Toda a geometria — avanço simples, avanço duplo na
/// estreia, captura só na diagonal — vive em <see cref="Move"/>, junto com a das outras peças. As
/// classes de peça guardam apenas cor e tipo, e é isso que este arquivo faz.
/// </para>
///
/// <para>
/// O motivo está explicado em <see cref="Piece"/>: a mesma geometria era descrita duas vezes, uma em
/// cada subclasse e outra em <see cref="Move"/> (que precisa dela para detectar xeque). Duas
/// descrições da mesma regra é uma que fica para trás.
/// </para>
///
/// <para>
/// O peão é o único tipo cujo movimento depende de estado: <see cref="Piece.HasAlreadyOneMove"/>
/// decide se o avanço duplo ainda está disponível.
/// </para>
/// </summary>
public class Pawn : Piece
{
    public Pawn(ColorEnum color)
    {
        Color = color;
        Type = PieceEnum.Pawn;
    }
}
