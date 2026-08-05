namespace Hibrygame.Enums;

/// <summary>
/// Tipo de peca. Serializado para o frontend como o nome do membro em PascalCase
/// ("Pawn", "Knight", ...) via ToString() em ChessHub.MapSquare; ver o tipo
/// PieceType em src/types/chess.ts.
/// </summary>
public enum PieceEnum
{
    Pawn,
    Bishop,
    Knight,
    Rook,
    Queen,
    King,
    None
}
