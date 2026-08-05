using Hibrygame.Enums;

namespace Hibrygame;

/// <summary>
/// Uma peca. Guarda apenas identidade e estado; a geometria de como cada tipo se move
/// vive inteiramente em <see cref="Move"/>.
///
/// <see cref="GetPossibleMove"/> era abstrato e cada subclasse repetia a sua lista de
/// direcoes e o seu alcance para passar a Move. Como Move ja precisava conhecer a
/// geometria de todos os tipos — para responder que casas cada peca ATACA, sem o que nao
/// se detecta xeque — a mesma informacao existia em dois lugares. Mudar uma e esquecer a
/// outra era um bug esperando acontecer, entao ficou so a de Move.
/// </summary>
public abstract class Piece
{
    public PieceEnum Type { get; set; } = PieceEnum.None;
    public ColorEnum Color { get; set; } = ColorEnum.None;

    /// <summary>Sincronizado por <see cref="Move.IsKingInCheck"/>; o snapshot enviado ao frontend expoe.</summary>
    public bool IsInCheckState { get; set; }

    /// <summary>Usado pelo peao (avanco duplo) e necessario para roque, se um dia existir.</summary>
    public bool HasAlreadyOneMove { get; set; }

    public (List<Position> possibleMoves, Piece? actualPieceTrigger) GetPossibleMove(Board board, Position pos)
        => Move.LegalMovesFor(board, pos);
}
