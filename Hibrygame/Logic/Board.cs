using Hibrygame.Enums;

namespace Hibrygame;

/// <summary>
/// O tabuleiro: uma grade 8x8 de <see cref="Position"/>.
///
/// <para>
/// <b>Leia isto antes de mexer em qualquer coisa da engine.</b> A convenção de coordenadas aqui é
/// contraintuitiva e é a origem da maior parte da confusão de quem chega:
/// </para>
/// <list type="bullet">
///   <item><description><c>Row</c> (0..7) é o <b>arquivo</b> — a coluna vertical do tabuleiro,
///   <c>a</c> a <c>h</c>. <c>Row = 0</c> é o arquivo <c>a</c>.</description></item>
///   <item><description><c>Column</c> (0..7) é a <b>fileira</b> — a linha horizontal, 8 a 1.
///   <c>Column = 0</c> é a fileira <b>8</b> e <c>Column = 7</c> é a fileira <b>1</b>.</description></item>
/// </list>
///
/// <para>
/// Ou seja: os nomes estão trocados em relação ao que se esperaria, e a fileira é numerada ao
/// contrário. As conversões são <c>arquivo = 'a' + Row</c> e <c>fileira = 8 - Column</c>. Assim,
/// <c>Column = 7</c> é a primeira fileira, onde ficam as peças <b>brancas</b>, e <c>Column = 0</c>
/// é a oitava, das <b>pretas</b>.
/// </para>
///
/// <para>
/// <b>Nunca faça essa conversão à mão.</b> Use <c>Position.FromAlgebraic</c>,
/// <c>Position.TryFromAlgebraic</c> e <c>Position.Algebraic</c>. Aritmética de coordenada espalhada
/// pelo código é o caminho mais curto para um bug de espelhamento — a fronteira externa (hub, DTO,
/// log) fala sempre notação algébrica (<c>"e2"</c>), e <c>Row</c>/<c>Column</c> ficam internos.
/// </para>
///
/// <para>
/// <b>Este objeto é mutável e compartilhado.</b> Cada sala tem um tabuleiro, e a avaliação de
/// legalidade simula lances nele — aplicando e desfazendo. Por isso todo acesso ao tabuleiro de uma
/// sala tem de passar por <c>GameRoom.Serialized</c>: duas chamadas concorrentes sem esse lock
/// corrompem o estado.
/// </para>
/// </summary>
public class Board
{
    /// <summary>
    /// As 64 casas, indexadas por <c>[Row, Column]</c> — nessa ordem, e ver a nota da classe sobre
    /// o que cada um significa.
    ///
    /// <para>
    /// Começa como um array de <c>null</c>: as casas só passam a existir depois de
    /// <see cref="StartBoard"/>. É por isso que quase todo laço sobre <c>Positions</c> nesta classe
    /// começa com <c>if (position is null) continue;</c>.
    /// </para>
    /// </summary>
    public Position[,] Positions { get; set; } = new Position[8, 8];

    /// <summary>
    /// Cria as 64 casas, vazias e sem cor definida.
    ///
    /// <para>
    /// <b>Primeiro dos dois passos de montagem.</b> Este só cria as casas; quem coloca as peças e
    /// pinta o tabuleiro é <see cref="MakePieceInInitialState"/>. Chamar apenas este deixa um
    /// tabuleiro sem peça nenhuma — o que é útil em teste que quer montar uma posição específica.
    /// </para>
    /// </summary>
    /// <returns>As casas criadas, em lista, para quem preferir iterar em vez de indexar.</returns>
    public List<Position> StartBoard()
    {
        var pos = new List<Position>();
        for (var r = 0; r < 8; r++)
        {
            for (var c = 0; c < 8; c++)
            {
                Positions[r, c] = new Position(r, c);
                pos.Add(Positions[r, c]);
            }
        }
        return pos;
    }

    /// <summary>
    /// Pinta as casas e coloca as 32 peças na posição inicial.
    ///
    /// <para>
    /// <b>Segundo passo:</b> exige que <see cref="StartBoard"/> já tenha rodado, senão não há casa
    /// nenhuma para preencher.
    /// </para>
    /// </summary>
    public void MakePieceInInitialState()
    {
        foreach (var position in Positions)
        {
            if (position is null) continue;

            // Soma ímpar é casa escura. A paridade de (arquivo + fileira) é o que faz o padrão
            // xadrez alternar, e independe de qual índice representa o quê.
            position.SquareColor = (position.Row + position.Column) % 2 == 1 ? ColorEnum.Black : ColorEnum.White;

            position.Piece = DefinePiece(position);
        }
    }

    /// <summary>
    /// Qual peça começa nesta casa, ou <c>null</c> se a casa começa vazia.
    ///
    /// <para>
    /// Tudo aqui é decidido por <c>Column</c>, porque <c>Column</c> é a fileira — ver a nota da
    /// classe. As quatro fileiras povoadas são: 7 (brancas de trás), 6 (peões brancos), 1 (peões
    /// pretos) e 0 (pretas de trás). As fileiras 2 a 5 ficam vazias.
    /// </para>
    ///
    /// <para>
    /// Dentro de uma fileira de trás, o <c>Row</c> — o arquivo — decide a peça, e a simetria do
    /// xadrez aparece nos pares: <c>0 or 7</c> são as torres (arquivos <c>a</c> e <c>h</c>),
    /// <c>1 or 6</c> os cavalos, <c>2 or 5</c> os bispos. Rainha em <c>3</c> (arquivo <c>d</c>) e rei
    /// em <c>4</c> (arquivo <c>e</c>) não têm par, e é essa assimetria que decide o lado do
    /// tabuleiro.
    /// </para>
    /// </summary>
    private static Piece? DefinePiece(Position pos)
    {
        // Fileira 1 — peças brancas de trás.
        if (pos.Column == 7)
        {
            return pos.Row switch
            {
                0 or 7 => new Rook(ColorEnum.White),
                1 or 6 => new Knight(ColorEnum.White),
                2 or 5 => new Bishop(ColorEnum.White),
                3 => new Queen(ColorEnum.White),
                4 => new King(ColorEnum.White),
                _ => null
            };
        }

        // Fileira 2 — peões brancos. Fileira 7 — peões pretos.
        if (pos.Column == 6) return new Pawn(ColorEnum.White);
        if (pos.Column == 1) return new Pawn(ColorEnum.Black);

        // Fileira 8 — peças pretas de trás, espelhando as brancas.
        if (pos.Column == 0)
        {
            return pos.Row switch
            {
                0 or 7 => new Rook(ColorEnum.Black),
                1 or 6 => new Knight(ColorEnum.Black),
                2 or 5 => new Bishop(ColorEnum.Black),
                3 => new Queen(ColorEnum.Black),
                4 => new King(ColorEnum.Black),
                _ => null
            };
        }

        // Fileiras 3 a 6 começam vazias.
        return null;
    }

    /// <summary>Casas que têm peça. Em posição inicial são 32; ao longo da partida, menos.</summary>
    public List<Position> GetPositionsPlaced()
    {
        var pos = new List<Position>();
        foreach (var position in Positions)
        {
            if (position?.Piece is null) continue;
            pos.Add(position);
        }
        return pos;
    }

    /// <summary>
    /// Todas as 64 casas, com peça ou sem. Usado por <c>Move.MakeMove</c> para limpar os destaques
    /// depois de um lance aplicado.
    /// </summary>
    public List<Position> GetAllSquares()
    {
        var pos = new List<Position>();
        foreach (var position in Positions)
        {
            if (position is null) continue;
            pos.Add(position);
        }
        return pos;
    }

    /// <summary>Casas ocupadas pelo adversario de <paramref name="color"/>.</summary>
    public List<Position> GetOpponentPositions(ColorEnum color)
    {
        var opponent = color == ColorEnum.Black ? ColorEnum.White : ColorEnum.Black;
        var pos = new List<Position>();
        foreach (var position in Positions)
        {
            if (position?.Piece?.Color == opponent) pos.Add(position);
        }
        return pos;
    }

    /// <summary>
    /// A casa em <paramref name="row"/>/<paramref name="column"/>.
    ///
    /// <para>
    /// <b>Não valida os índices</b> — fora de 0..7 estoura
    /// <see cref="IndexOutOfRangeException"/>. Quem recebe coordenada de fora deve validar antes,
    /// com <c>Position.TryFromAlgebraic</c> ou <c>Position.IsInsideBoard</c>.
    /// </para>
    /// </summary>
    public Position GetPositionInBoard(int row, int column)
    {
        return Positions[row, column];
    }

    /// <summary>
    /// A casa correspondente a uma notação algébrica como <c>"e4"</c>.
    ///
    /// <para>
    /// Usa <c>Position.ToIndices</c>, que <b>lança</b> em notação inválida. Onde a entrada vem do
    /// cliente — como no hub — prefira <c>Position.TryFromAlgebraic</c>, que devolve <c>false</c> em
    /// vez de estourar.
    /// </para>
    /// </summary>
    public Position GetPositionByAlgebraic(string notation)
    {
        var (row, column) = Position.ToIndices(notation);
        return Positions[row, column];
    }

    // Removidos por nao terem chamador:
    //   GetPositionsPlacedInBoard()  era so um alias de GetPositionsPlaced()
    //   MakeMove(board, ...)         metodo de instancia que recebia o tabuleiro por
    //                                parametro e ignorava `this`. Todos os chamadores
    //                                sempre usaram Move.MakeMove direto.
}
