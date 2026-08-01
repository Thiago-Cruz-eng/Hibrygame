using Hibrygame.Enums;
using Xunit;

namespace Hibrygame.Test.Hibrygame;

/// <summary>
/// Rede de seguranca do motor de regras (Fase 1 do refactor).
///
/// Cada teste declara o comportamento CORRETO de xadrez, nao o comportamento atual.
/// Um teste vermelho aqui e um bug real do motor, e nao um teste errado.
///
/// Escopo acordado: geracao/validacao de movimento por peca, rejeicao de movimento
/// que expoe o proprio rei, deteccao de xeque, pureza da geracao de movimento.
/// Roque, en passant, promocao, xeque-mate, afogamento e empates NAO existem no
/// motor e ficaram fora deste escopo por decisao explicita.
/// </summary>
public class MoveGenerationRegressionTests
{
    // ---------------------------------------------------------------------
    // Posicao inicial — ancora. Qualquer motor de xadrez tem de acertar isto.
    // ---------------------------------------------------------------------

    [Fact]
    public void InitialPosition_WhiteHasExactlyTwentyLegalMoves()
    {
        var board = TestBoards.NewGame();

        var total = board.OccupiedBy(ColorEnum.White).Sum(square => board.MovesFrom(square).Length);

        Assert.Equal(20, total);
    }

    [Fact]
    public void InitialPosition_BlackHasExactlyTwentyLegalMoves()
    {
        var board = TestBoards.NewGame();

        var total = board.OccupiedBy(ColorEnum.Black).Sum(square => board.MovesFrom(square).Length);

        Assert.Equal(20, total);
    }

    [Theory]
    // Cavalos saltam sobre a fileira de peoes.
    [InlineData("b1", new[] { "a3", "c3" })]
    [InlineData("g1", new[] { "f3", "h3" })]
    [InlineData("b8", new[] { "a6", "c6" })]
    [InlineData("g8", new[] { "f6", "h6" })]
    // Peoes avancam uma ou duas casas.
    [InlineData("e2", new[] { "e3", "e4" })]
    [InlineData("a2", new[] { "a3", "a4" })]
    [InlineData("h2", new[] { "h3", "h4" })]
    [InlineData("e7", new[] { "e5", "e6" })]
    // Todo o resto esta trancado atras dos proprios peoes.
    [InlineData("a1", new string[0])]
    [InlineData("c1", new string[0])]
    [InlineData("d1", new string[0])]
    [InlineData("e1", new string[0])]
    [InlineData("h1", new string[0])]
    [InlineData("d8", new string[0])]
    [InlineData("e8", new string[0])]
    public void InitialPosition_PieceOffersExactlyTheseSquares(string from, string[] expected)
    {
        var board = TestBoards.NewGame();

        Assert.Equal(TestBoards.Squares(expected), board.MovesFrom(from));
    }

    // ---------------------------------------------------------------------
    // Peao
    // ---------------------------------------------------------------------

    [Fact]
    public void Pawn_White_FromStartingRank_AdvancesOneOrTwo()
    {
        var board = TestBoards.With(("e2", new Pawn(ColorEnum.White)));

        Assert.Equal(TestBoards.Squares("e3", "e4"), board.MovesFrom("e2"));
    }

    [Fact]
    public void Pawn_White_AfterItsFirstMove_AdvancesOnlyOne()
    {
        var pawn = new Pawn(ColorEnum.White) { HasAlreadyOneMove = true };
        var board = TestBoards.With(("e4", pawn));

        Assert.Equal(TestBoards.Squares("e5"), board.MovesFrom("e4"));
    }

    [Fact]
    public void Pawn_Black_MovesTowardRankOne()
    {
        var board = TestBoards.With(("e7", new Pawn(ColorEnum.Black)));

        Assert.Equal(TestBoards.Squares("e5", "e6"), board.MovesFrom("e7"));
    }

    [Fact]
    public void Pawn_CannotCaptureStraightAhead()
    {
        // Peao branco em e4, peao preto bloqueando e5. Peao NAO captura para frente:
        // a casa esta bloqueada e nao ha jogada nenhuma.
        var board = TestBoards.With(
            ("e4", new Pawn(ColorEnum.White) { HasAlreadyOneMove = true }),
            ("e5", new Pawn(ColorEnum.Black)));

        Assert.Empty(board.MovesFrom("e4"));
    }

    [Fact]
    public void Pawn_CannotCaptureTwoSquaresAhead()
    {
        // Avanco duplo em casa ocupada pelo inimigo nao e captura nem avanco.
        var board = TestBoards.With(
            ("e2", new Pawn(ColorEnum.White)),
            ("e4", new Rook(ColorEnum.Black)));

        Assert.Equal(TestBoards.Squares("e3"), board.MovesFrom("e2"));
    }

    [Fact]
    public void Pawn_CannotJumpOverAPieceOnTheDoubleAdvance()
    {
        // e3 ocupado por inimigo: o peao nao avanca nem uma nem duas casas.
        var board = TestBoards.With(
            ("e2", new Pawn(ColorEnum.White)),
            ("e3", new Knight(ColorEnum.Black)));

        Assert.Empty(board.MovesFrom("e2"));
    }

    [Fact]
    public void Pawn_CapturesDiagonallyForward()
    {
        var board = TestBoards.With(
            ("e4", new Pawn(ColorEnum.White) { HasAlreadyOneMove = true }),
            ("d5", new Pawn(ColorEnum.Black)),
            ("f5", new Pawn(ColorEnum.Black)));

        Assert.Equal(TestBoards.Squares("d5", "e5", "f5"), board.MovesFrom("e4"));
    }

    [Fact]
    public void Pawn_Black_CapturesDiagonallyForward()
    {
        var board = TestBoards.With(
            ("e5", new Pawn(ColorEnum.Black) { HasAlreadyOneMove = true }),
            ("d4", new Pawn(ColorEnum.White)),
            ("f4", new Pawn(ColorEnum.White)));

        Assert.Equal(TestBoards.Squares("d4", "e4", "f4"), board.MovesFrom("e5"));
    }

    [Fact]
    public void Pawn_DoesNotMoveDiagonallyToAnEmptySquare()
    {
        var board = TestBoards.With(("e4", new Pawn(ColorEnum.White) { HasAlreadyOneMove = true }));

        Assert.Equal(TestBoards.Squares("e5"), board.MovesFrom("e4"));
    }

    [Fact]
    public void Pawn_DoesNotCaptureItsOwnColourDiagonally()
    {
        var board = TestBoards.With(
            ("e4", new Pawn(ColorEnum.White) { HasAlreadyOneMove = true }),
            ("d5", new Pawn(ColorEnum.White)),
            ("f5", new Pawn(ColorEnum.White)));

        Assert.Equal(TestBoards.Squares("e5"), board.MovesFrom("e4"));
    }

    // ---------------------------------------------------------------------
    // Cavalo
    // ---------------------------------------------------------------------

    [Fact]
    public void Knight_FromCentre_HasEightMoves()
    {
        var board = TestBoards.With(("e4", new Knight(ColorEnum.White)));

        Assert.Equal(
            TestBoards.Squares("c3", "c5", "d2", "d6", "f2", "f6", "g3", "g5"),
            board.MovesFrom("e4"));
    }

    [Fact]
    public void Knight_FromCorner_HasTwoMoves()
    {
        var board = TestBoards.With(("a1", new Knight(ColorEnum.White)));

        Assert.Equal(TestBoards.Squares("b3", "c2"), board.MovesFrom("a1"));
    }

    [Fact]
    public void Knight_CapturesEnemyOnDestination()
    {
        var board = TestBoards.With(
            ("e4", new Knight(ColorEnum.White)),
            ("f6", new Pawn(ColorEnum.Black)));

        Assert.Contains("f6", board.MovesFrom("e4"));
    }

    [Fact]
    public void Knight_CannotLandOnItsOwnPiece()
    {
        var board = TestBoards.With(
            ("e4", new Knight(ColorEnum.White)),
            ("f6", new Pawn(ColorEnum.White)));

        Assert.DoesNotContain("f6", board.MovesFrom("e4"));
    }

    [Fact]
    public void Knight_JumpsOverPiecesRegardlessOfWhatIsBetween()
    {
        // Cavalo em b1 cercado por pecas amigas: continua alcancando a3, c3 e d2,
        // que sao os tres destinos em L geometricamente validos a partir de b1.
        var board = TestBoards.With(
            ("b1", new Knight(ColorEnum.White)),
            ("b2", new Pawn(ColorEnum.White)),
            ("a1", new Rook(ColorEnum.White)),
            ("c1", new Bishop(ColorEnum.White)),
            ("a2", new Pawn(ColorEnum.White)),
            ("c2", new Pawn(ColorEnum.White)));

        Assert.Equal(TestBoards.Squares("a3", "c3", "d2"), board.MovesFrom("b1"));
    }

    [Fact]
    public void Knight_GeneratingMoves_DoesNotRemoveThePieceItJumpsPast()
    {
        // Regressao: gerar os movimentos do cavalo apagava a peca inimiga que
        // estivesse na casa intermediaria do calculo.
        var board = TestBoards.With(
            ("b1", new Knight(ColorEnum.White)),
            ("b3", new Pawn(ColorEnum.Black)));

        board.MovesFrom("b1");

        var survivor = board.PieceAt("b3");
        Assert.NotNull(survivor);
        Assert.Equal(PieceEnum.Pawn, survivor!.Type);
        Assert.Equal(ColorEnum.Black, survivor.Color);
    }

    // ---------------------------------------------------------------------
    // Pecas de alcance longo
    // ---------------------------------------------------------------------

    [Fact]
    public void Rook_OnEmptyBoard_ControlsItsRankAndFile()
    {
        var board = TestBoards.With(("d4", new Rook(ColorEnum.White)));

        Assert.Equal(
            TestBoards.Squares(
                "d1", "d2", "d3", "d5", "d6", "d7", "d8",
                "a4", "b4", "c4", "e4", "f4", "g4", "h4"),
            board.MovesFrom("d4"));
    }

    [Fact]
    public void Rook_StopsBeforeItsOwnPiece()
    {
        var board = TestBoards.With(
            ("d4", new Rook(ColorEnum.White)),
            ("d6", new Pawn(ColorEnum.White)));

        var moves = board.MovesFrom("d4");

        Assert.Contains("d5", moves);
        Assert.DoesNotContain("d6", moves);
        Assert.DoesNotContain("d7", moves);
    }

    [Fact]
    public void Rook_CapturesFirstEnemyAndStopsThere()
    {
        var board = TestBoards.With(
            ("d4", new Rook(ColorEnum.White)),
            ("d6", new Pawn(ColorEnum.Black)));

        var moves = board.MovesFrom("d4");

        Assert.Contains("d5", moves);
        Assert.Contains("d6", moves);
        Assert.DoesNotContain("d7", moves);
        Assert.DoesNotContain("d8", moves);
    }

    [Fact]
    public void Bishop_OnEmptyBoard_ControlsBothDiagonals()
    {
        var board = TestBoards.With(("d4", new Bishop(ColorEnum.White)));

        Assert.Equal(
            TestBoards.Squares(
                "a1", "b2", "c3", "e5", "f6", "g7", "h8",
                "a7", "b6", "c5", "e3", "f2", "g1"),
            board.MovesFrom("d4"));
    }

    [Fact]
    public void Queen_OnEmptyBoard_HasTwentySevenMoves()
    {
        var board = TestBoards.With(("d4", new Queen(ColorEnum.White)));

        Assert.Equal(27, board.MovesFrom("d4").Length);
    }

    // ---------------------------------------------------------------------
    // Rei
    // ---------------------------------------------------------------------

    [Fact]
    public void King_OnEmptyBoard_ReachesItsEightNeighbours()
    {
        var board = TestBoards.With(("d4", new King(ColorEnum.White)));

        Assert.Equal(
            TestBoards.Squares("c3", "c4", "c5", "d3", "d5", "e3", "e4", "e5"),
            board.MovesFrom("d4"));
    }

    [Fact]
    public void King_IsNotOfferedASquareAttackedByTheEnemy()
    {
        // Torre preta em d8 controla toda a coluna d. O rei branco em e1 nao pode
        // andar para d1 nem d2.
        var board = TestBoards.With(
            ("e1", new King(ColorEnum.White)),
            ("d8", new Rook(ColorEnum.Black)));

        var moves = board.MovesFrom("e1");

        Assert.DoesNotContain("d1", moves);
        Assert.DoesNotContain("d2", moves);
    }

    [Fact]
    public void King_CannotCaptureAProtectedPiece()
    {
        // Peao preto em d2 esta defendido pela torre em d8: o rei nao pode captura-lo.
        var board = TestBoards.With(
            ("e1", new King(ColorEnum.White)),
            ("d2", new Pawn(ColorEnum.Black)),
            ("d8", new Rook(ColorEnum.Black)));

        Assert.DoesNotContain("d2", board.MovesFrom("e1"));
    }

    // ---------------------------------------------------------------------
    // Geracao de movimento e leitura pura — nao pode alterar a partida
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("a1")]
    [InlineData("b1")]
    [InlineData("c1")]
    [InlineData("d1")]
    [InlineData("e1")]
    [InlineData("g1")]
    [InlineData("e2")]
    [InlineData("b8")]
    [InlineData("e7")]
    public void GetPossibleMove_OnTheInitialBoard_LeavesEveryPieceWhereItWas(string from)
    {
        var board = TestBoards.NewGame();
        var before = board.Fingerprint();

        board.MovesFrom(from);

        Assert.Equal(before, board.Fingerprint());
    }

    [Fact]
    public void GetPossibleMove_ForEveryPieceOnTheBoard_NeverChangesThePosition()
    {
        var board = TestBoards.NewGame();
        var before = board.Fingerprint();

        foreach (var square in board.OccupiedBy(ColorEnum.White).Concat(board.OccupiedBy(ColorEnum.Black)))
            board.MovesFrom(square);

        Assert.Equal(before, board.Fingerprint());
    }

    // ---------------------------------------------------------------------
    // Deteccao de xeque
    // ---------------------------------------------------------------------

    [Fact]
    public async Task IsKingInCheck_TrueWhenARookAttacksAlongTheFile()
    {
        var board = TestBoards.With(
            ("e1", new King(ColorEnum.White)),
            ("e8", new Rook(ColorEnum.Black)));

        Assert.True(await Move.IsKingInCheck(board, ColorEnum.White));
    }

    [Fact]
    public async Task IsKingInCheck_FalseWhenThatAttackIsBlocked()
    {
        var board = TestBoards.With(
            ("e1", new King(ColorEnum.White)),
            ("e4", new Pawn(ColorEnum.White)),
            ("e8", new Rook(ColorEnum.Black)));

        Assert.False(await Move.IsKingInCheck(board, ColorEnum.White));
    }

    [Fact]
    public async Task IsKingInCheck_TrueWhenABishopAttacksAlongTheDiagonal()
    {
        var board = TestBoards.With(
            ("e1", new King(ColorEnum.White)),
            ("a5", new Bishop(ColorEnum.Black)));

        Assert.True(await Move.IsKingInCheck(board, ColorEnum.White));
    }

    [Fact]
    public async Task IsKingInCheck_TrueWhenAPawnAttacksDiagonally()
    {
        var board = TestBoards.With(
            ("e1", new King(ColorEnum.White)),
            ("d2", new Pawn(ColorEnum.Black)));

        Assert.True(await Move.IsKingInCheck(board, ColorEnum.White));
    }

    [Fact]
    public async Task IsKingInCheck_TrueWhenAKnightAttacks()
    {
        // Cavalo preto em f3 da xeque ao rei branco em e1.
        var board = TestBoards.With(
            ("e1", new King(ColorEnum.White)),
            ("f3", new Knight(ColorEnum.Black)));

        Assert.True(await Move.IsKingInCheck(board, ColorEnum.White));
    }

    [Fact]
    public async Task IsKingInCheck_FalseWhenNothingAttacksTheKing()
    {
        var board = TestBoards.With(
            ("e1", new King(ColorEnum.White)),
            ("a8", new Rook(ColorEnum.Black)));

        Assert.False(await Move.IsKingInCheck(board, ColorEnum.White));
    }

    [Fact]
    public async Task IsKingInCheck_FalseOnTheInitialPosition()
    {
        var board = TestBoards.NewGame();

        Assert.False(await Move.IsKingInCheck(board, ColorEnum.White));
        Assert.False(await Move.IsKingInCheck(board, ColorEnum.Black));
    }

    [Fact]
    public void GeneratingMoves_WhenTheEnemyKingHasNoEscapeSquare_DoesNotThrow()
    {
        // Rei branco em a1 preso pelos proprios peoes; torre preta em h1 da xeque
        // pela primeira fileira e cobre a unica fuga (b1).
        var board = TestBoards.With(
            ("a1", new King(ColorEnum.White)),
            ("a2", new Pawn(ColorEnum.White)),
            ("b2", new Pawn(ColorEnum.White)),
            ("h1", new Rook(ColorEnum.Black)));

        var rook = board.PieceAt("h1")!;

        var moves = rook.GetPossibleMove(board, board.At("h1"));

        Assert.NotNull(moves.possibleMoves);
    }

    // ---------------------------------------------------------------------
    // Movimento que expoe o proprio rei
    // ---------------------------------------------------------------------

    [Fact]
    public async Task MakeMove_RejectsAMoveThatWouldExposeItsOwnKing()
    {
        // Torre branca em e2 esta cravada: e o unico bloqueio entre o rei em e1
        // e a torre preta em e8. Sair da coluna e e ilegal.
        var board = TestBoards.With(
            ("e1", new King(ColorEnum.White)),
            ("e2", new Rook(ColorEnum.White)),
            ("e8", new Rook(ColorEnum.Black)));

        var source = board.At("e2");
        var (moves, _) = source.Piece!.GetPossibleMove(board, source);

        var applied = await Move.MakeMove(board, moves, board.At("a2"), source);

        Assert.False(applied);
    }

    [Fact]
    public async Task MakeMove_WhenItRejectsAMove_LeavesTheBoardExactlyAsItWas()
    {
        var board = TestBoards.With(
            ("e1", new King(ColorEnum.White)),
            ("e2", new Rook(ColorEnum.White)),
            ("e8", new Rook(ColorEnum.Black)));

        var source = board.At("e2");
        var (moves, _) = source.Piece!.GetPossibleMove(board, source);
        var before = board.Fingerprint();

        await Move.MakeMove(board, moves, board.At("a2"), source);

        Assert.Equal(before, board.Fingerprint());
    }

    [Fact]
    public void PinnedPiece_IsNotOfferedMovesThatWouldExposeItsKing()
    {
        // A torre cravada em e2 só pode mover-se dentro da coluna e.
        var board = TestBoards.With(
            ("e1", new King(ColorEnum.White)),
            ("e2", new Rook(ColorEnum.White)),
            ("e8", new Rook(ColorEnum.Black)));

        var moves = board.MovesFrom("e2");

        Assert.All(moves, square => Assert.StartsWith("e", square));
    }

    [Fact]
    public async Task MakeMove_AllowsThePinnedPieceToMoveAlongThePinLine()
    {
        var board = TestBoards.With(
            ("e1", new King(ColorEnum.White)),
            ("e2", new Rook(ColorEnum.White)),
            ("e8", new Rook(ColorEnum.Black)));

        var source = board.At("e2");
        var (moves, _) = source.Piece!.GetPossibleMove(board, source);

        var applied = await Move.MakeMove(board, moves, board.At("e4"), source);

        Assert.True(applied);
        Assert.Equal(PieceEnum.Rook, board.PieceAt("e4")!.Type);
        Assert.Null(board.PieceAt("e2"));
    }

    [Fact]
    public async Task MakeMove_MovesThePieceAndClearsTheOriginSquare()
    {
        var board = TestBoards.With(("d4", new Rook(ColorEnum.White)));

        var source = board.At("d4");
        var (moves, _) = source.Piece!.GetPossibleMove(board, source);

        Assert.True(await Move.MakeMove(board, moves, board.At("d8"), source));
        Assert.Null(board.PieceAt("d4"));
        Assert.Equal(PieceEnum.Rook, board.PieceAt("d8")!.Type);
    }

    [Fact]
    public async Task MakeMove_CapturingReplacesTheEnemyPiece()
    {
        var board = TestBoards.With(
            ("d4", new Rook(ColorEnum.White)),
            ("d8", new Pawn(ColorEnum.Black)));

        var source = board.At("d4");
        var (moves, _) = source.Piece!.GetPossibleMove(board, source);

        Assert.True(await Move.MakeMove(board, moves, board.At("d8"), source));
        Assert.Equal(ColorEnum.White, board.PieceAt("d8")!.Color);
        Assert.Equal(PieceEnum.Rook, board.PieceAt("d8")!.Type);
    }

    // ---------------------------------------------------------------------
    // Posicoes classicamente quebra-motor (Fase 2)
    // ---------------------------------------------------------------------

    [Fact]
    public void DoubleCheck_LeavesNoMoveForAnyPieceExceptTheKing()
    {
        // Rei branco em e1 sob xeque duplo: torre preta em e8 pela coluna e,
        // bispo preto em a5 pela diagonal a5-e1. Em xeque duplo nao existe bloqueio
        // que sirva: tapar a coluna deixa a diagonal aberta e vice-versa.
        var board = TestBoards.With(
            ("e1", new King(ColorEnum.White)),
            ("h2", new Rook(ColorEnum.White)),
            ("e8", new Rook(ColorEnum.Black)),
            ("a5", new Bishop(ColorEnum.Black)));

        Assert.Empty(board.MovesFrom("h2"));
    }

    [Fact]
    public void SingleCheck_AllowsOnlyTheMoveThatBlocksIt()
    {
        // Torre preta em e8 da xeque. O cavalo em g1 alcanca h3, f3 e e2, mas apenas
        // e2 intercepta a coluna e.
        var board = TestBoards.With(
            ("e1", new King(ColorEnum.White)),
            ("g1", new Knight(ColorEnum.White)),
            ("e8", new Rook(ColorEnum.Black)));

        Assert.Equal(TestBoards.Squares("e2"), board.MovesFrom("g1"));
    }

    [Fact]
    public void PinnedPiece_MayCaptureThePieceThatPinsIt()
    {
        // A torre cravada em e4 pode capturar a torre que a crava, em e8:
        // continua na linha da cravada e elimina o atacante.
        var board = TestBoards.With(
            ("e1", new King(ColorEnum.White)),
            ("e4", new Rook(ColorEnum.White)),
            ("e8", new Rook(ColorEnum.Black)));

        Assert.Contains("e8", board.MovesFrom("e4"));
    }

    [Fact]
    public void CapturingTheCheckingPiece_IsALegalWayOutOfCheck()
    {
        // Torre preta em h1 da xeque pela primeira fileira; a dama branca em h5
        // captura-a pela coluna h e resolve o xeque.
        var board = TestBoards.With(
            ("a1", new King(ColorEnum.White)),
            ("h5", new Queen(ColorEnum.White)),
            ("h1", new Rook(ColorEnum.Black)));

        Assert.Contains("h1", board.MovesFrom("h5"));
    }

    [Fact]
    public void TwoKings_MayNotStandNextToEachOther()
    {
        // Rei preto em e3 cobre d2, e2 e f2, entao o rei branco em e1 so tem d1 e f1.
        var board = TestBoards.With(
            ("e1", new King(ColorEnum.White)),
            ("e3", new King(ColorEnum.Black)));

        Assert.Equal(TestBoards.Squares("d1", "f1"), board.MovesFrom("e1"));
    }

    [Fact]
    public async Task PromotionRankIsReached_ButThePawnStaysAPawn()
    {
        // Documenta a ausencia de promocao, que ficou fora do escopo acordado: o peao
        // chega a oitava fileira e continua peao, sem lance nenhum a partir dali.
        // Se a promocao for implementada, este teste tem de mudar.
        var board = TestBoards.With(
            ("e1", new King(ColorEnum.White)),
            ("e7", new Pawn(ColorEnum.White) { HasAlreadyOneMove = true }),
            ("a8", new King(ColorEnum.Black)));

        var source = board.At("e7");
        var (moves, _) = source.Piece!.GetPossibleMove(board, source);
        await Move.MakeMove(board, moves, board.At("e8"), source);

        Assert.Equal(PieceEnum.Pawn, board.PieceAt("e8")!.Type);
        Assert.Empty(board.MovesFrom("e8"));
    }

    [Fact]
    public async Task MakeMove_RejectsATargetThatIsNotAPossibleMove()
    {
        var board = TestBoards.With(("d4", new Rook(ColorEnum.White)));

        var source = board.At("d4");
        var (moves, _) = source.Piece!.GetPossibleMove(board, source);

        // e5 e diagonal: nunca e movimento de torre.
        Assert.False(await Move.MakeMove(board, moves, board.At("e5"), source));
        Assert.Equal(PieceEnum.Rook, board.PieceAt("d4")!.Type);
    }
}
