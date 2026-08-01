using Hibrygame.Enums;
using Xunit;

namespace Hibrygame.Test.Hibrygame;

/// <summary>
/// Deteccao de fim de partida.
///
/// Xeque-mate e "o jogador da vez nao tem nenhum lance legal, e esta em xeque"; afogamento
/// e a mesma coisa sem o xeque. As duas metades ja existiam no motor separadas — o que
/// faltava era junta-las e o hub encerrar a partida.
/// </summary>
public class GameOutcomeTests
{
    // -----------------------------------------------------------------
    // Em andamento
    // -----------------------------------------------------------------

    [Fact]
    public void InitialPosition_IsInProgressForBothColours()
    {
        var board = TestBoards.NewGame();

        Assert.Equal(GameOutcome.InProgress, Move.EvaluateOutcome(board, ColorEnum.White));
        Assert.Equal(GameOutcome.InProgress, Move.EvaluateOutcome(board, ColorEnum.Black));
    }

    [Fact]
    public void BeingInCheckWithAnEscape_IsNotCheckmate()
    {
        // Torre preta em e8 da xeque ao rei branco em e1, que escapa para d1 ou f1.
        var board = TestBoards.With(
            ("e1", new King(ColorEnum.White)),
            ("e8", new Rook(ColorEnum.Black)));

        Assert.Equal(GameOutcome.InProgress, Move.EvaluateOutcome(board, ColorEnum.White));
    }

    // -----------------------------------------------------------------
    // Xeque-mate
    // -----------------------------------------------------------------

    [Fact]
    public void BackRankMate_IsCheckmate()
    {
        // Rei preto em g8 trancado pelos proprios peoes em f7, g7 e h7; torre branca em e8
        // da xeque pela oitava fileira. f8 e h8 seguem atacadas pela torre depois de o rei
        // sair de g8, entao nao ha fuga, bloqueio nem captura.
        var board = TestBoards.With(
            ("g8", new King(ColorEnum.Black)),
            ("f7", new Pawn(ColorEnum.Black) { HasAlreadyOneMove = true }),
            ("g7", new Pawn(ColorEnum.Black) { HasAlreadyOneMove = true }),
            ("h7", new Pawn(ColorEnum.Black) { HasAlreadyOneMove = true }),
            ("e8", new Rook(ColorEnum.White)),
            ("a1", new King(ColorEnum.White)));

        Assert.Equal(GameOutcome.Checkmate, Move.EvaluateOutcome(board, ColorEnum.Black));

        // E as brancas, na vez delas, seguem jogando normalmente.
        Assert.Equal(GameOutcome.InProgress, Move.EvaluateOutcome(board, ColorEnum.White));
    }

    [Fact]
    public void CornerMateByTwoRooks_IsCheckmate()
    {
        // Mesma posicao de KingTests: rei branco em h1, torres pretas em g1, g2 e h2,
        // cada captura possivel caindo em casa defendida pela outra torre.
        var board = TestBoards.With(
            ("h1", new King(ColorEnum.White)),
            ("g1", new Rook(ColorEnum.Black)),
            ("g2", new Rook(ColorEnum.Black)),
            ("h2", new Rook(ColorEnum.Black)),
            ("a8", new King(ColorEnum.Black)));

        Assert.Equal(GameOutcome.Checkmate, Move.EvaluateOutcome(board, ColorEnum.White));
    }

    [Fact]
    public void CheckThatCanBeBlocked_IsNotCheckmate()
    {
        // Igual ao mate da primeira fileira, mas com um cavalo preto em g6, que bloqueia
        // em f8 e salva.
        var board = TestBoards.With(
            ("g8", new King(ColorEnum.Black)),
            ("f7", new Pawn(ColorEnum.Black) { HasAlreadyOneMove = true }),
            ("g7", new Pawn(ColorEnum.Black) { HasAlreadyOneMove = true }),
            ("h7", new Pawn(ColorEnum.Black) { HasAlreadyOneMove = true }),
            ("g6", new Knight(ColorEnum.Black)),
            ("e8", new Rook(ColorEnum.White)),
            ("a1", new King(ColorEnum.White)));

        Assert.Equal(GameOutcome.InProgress, Move.EvaluateOutcome(board, ColorEnum.Black));
    }

    // -----------------------------------------------------------------
    // Afogamento (empate)
    // -----------------------------------------------------------------

    [Fact]
    public void KingAndPawnStalemate_IsStalemate()
    {
        // Posicao classica: rei preto em c8, rei branco em c6, peao branco em c7.
        // O rei preto NAO esta em xeque — o peao ataca b8 e d8, nao c8 — e nao tem
        // nenhuma casa livre: b8 e d8 atacadas pelo peao, b7 e d7 pelo rei, e c7 esta
        // defendido pelo rei branco.
        var board = TestBoards.With(
            ("c8", new King(ColorEnum.Black)),
            ("c7", new Pawn(ColorEnum.White) { HasAlreadyOneMove = true }),
            ("c6", new King(ColorEnum.White)));

        Assert.False(Move.IsKingInCheck(board, ColorEnum.Black));
        Assert.Equal(GameOutcome.Stalemate, Move.EvaluateOutcome(board, ColorEnum.Black));
    }

    [Fact]
    public void StalemateIsNotReportedAsCheckmate()
    {
        var board = TestBoards.With(
            ("c8", new King(ColorEnum.Black)),
            ("c7", new Pawn(ColorEnum.White) { HasAlreadyOneMove = true }),
            ("c6", new King(ColorEnum.White)));

        Assert.NotEqual(GameOutcome.Checkmate, Move.EvaluateOutcome(board, ColorEnum.Black));
    }

    // -----------------------------------------------------------------
    // Pureza — avaliar o resultado nao altera o tabuleiro
    // -----------------------------------------------------------------

    [Fact]
    public void EvaluateOutcome_DoesNotChangeThePosition()
    {
        var board = TestBoards.NewGame();
        var before = board.Fingerprint();

        Move.EvaluateOutcome(board, ColorEnum.White);
        Move.EvaluateOutcome(board, ColorEnum.Black);

        Assert.Equal(before, board.Fingerprint());
    }

    [Fact]
    public void HasAnyLegalMove_MatchesTheSumOfEveryPieceMoves()
    {
        var board = TestBoards.NewGame();

        Assert.True(Move.HasAnyLegalMove(board, ColorEnum.White));
        Assert.True(Move.HasAnyLegalMove(board, ColorEnum.Black));

        var boxedIn = TestBoards.With(
            ("h1", new King(ColorEnum.White)),
            ("g1", new Rook(ColorEnum.Black)),
            ("g2", new Rook(ColorEnum.Black)),
            ("h2", new Rook(ColorEnum.Black)),
            ("a8", new King(ColorEnum.Black)));

        Assert.False(Move.HasAnyLegalMove(boxedIn, ColorEnum.White));
    }
}
