namespace Hibrygame.Enums;

/// <summary>
/// Direcao de deslocamento no tabuleiro, usada apenas internamente pela geracao de
/// movimento. Nunca cruza a fronteira da API — o frontend recebe casas em notacao
/// algebraica, nao direcoes.
///
/// North aumenta a fileira (rumo a fileira 8) e East aumenta a coluna (rumo ao
/// arquivo h). O mapeamento para os indices Row/Column esta em Move.Delta.
/// </summary>
public enum Direction
{
    North,
    South,
    East,
    West,
    NorthEast,
    SouthEast,
    NorthWest,
    SouthWest,
}
