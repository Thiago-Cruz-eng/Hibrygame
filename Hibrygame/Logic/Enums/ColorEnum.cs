namespace Hibrygame.Enums;

/// <summary>
/// Cor de peca e de casa.
///
/// O contrato com o frontend e o nome do membro em PascalCase ("White", "Black",
/// "None"), produzido por ToString() em ChessHub.MapSquare e consumido pelo tipo
/// Color em src/types/chess.ts. Nao anexar atributos de serializacao aqui: os que
/// existiam eram inertes — nenhum serializador do projeto os lia — e traziam
/// valores errados, com White marcado como "bhite" e None como "white".
/// </summary>
public enum ColorEnum
{
    Black,
    White,
    None
}
