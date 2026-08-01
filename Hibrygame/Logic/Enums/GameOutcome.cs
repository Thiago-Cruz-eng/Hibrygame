namespace Hibrygame.Enums;

/// <summary>
/// Situacao da partida do ponto de vista do jogador que tem a vez.
///
/// Serializado para o frontend como o nome do membro em PascalCase, igual as outras
/// enums do motor — ver o tipo GameOutcome em src/types/chess.ts.
/// </summary>
public enum GameOutcome
{
    /// <summary>O jogador da vez tem ao menos um lance legal.</summary>
    InProgress,

    /// <summary>Sem lance legal e em xeque: o adversario ganhou.</summary>
    Checkmate,

    /// <summary>Sem lance legal e sem estar em xeque: empate por afogamento.</summary>
    Stalemate
}
