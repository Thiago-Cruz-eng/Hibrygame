using Microsoft.AspNetCore.SignalR;
using Moq;
using Orchestrator.Infra.SignalR;
using Xunit;

namespace Orchestrator.Test;

/// <summary>
/// Regressoes da camada de tempo real encontradas na Fase 2 do refactor.
///
/// Cobre o que ChessHubTests nao alcancava: a flag AlreadyExisted de CreateRoom,
/// GetPossibleMoves (que nao tinha teste nenhum) e o acesso concorrente ao
/// tabuleiro compartilhado de uma sala.
/// </summary>
public class ChessHubRegressionTests
{
    private static string NewRoomName() => $"regr-{Guid.NewGuid()}";

    private static ChessHub CreateHub(string connectionId)
    {
        var clients = new Mock<IHubCallerClients>();
        var caller = new Mock<ISingleClientProxy>();
        var group = new Mock<IClientProxy>();
        var groups = new Mock<IGroupManager>();
        var context = new Mock<HubCallerContext>();

        context.Setup(c => c.ConnectionId).Returns(connectionId);
        clients.Setup(c => c.Caller).Returns(caller.Object);
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(group.Object);
        clients.Setup(c => c.All).Returns(group.Object);

        groups.Setup(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        groups.Setup(g => g.RemoveFromGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        caller.Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        group.Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new ChessHub
        {
            Clients = clients.Object,
            Groups = groups.Object,
            Context = context.Object
        };
    }

    /// <summary>Sala com dois jogadores e partida iniciada. Devolve os dois hubs.</summary>
    private static async Task<(string room, ChessHub white, ChessHub black)> StartedGame()
    {
        var room = NewRoomName();
        var white = CreateHub("conn-white");
        var black = CreateHub("conn-black");

        await white.CreateRoom(room);
        await white.JoinRoom("Branca", room);
        await black.JoinRoom("Preta", room);
        await white.StartGame(room);

        return (room, white, black);
    }

    private static string FingerprintOf(ChessHub.BoardSnapshot snapshot) =>
        string.Join("|", snapshot.Squares
            .OrderBy(s => s.Algebraic, StringComparer.Ordinal)
            .Select(s => $"{s.Algebraic}:{s.Piece?.Color}:{s.Piece?.Type}"));

    // ---------------------------------------------------------------
    // CreateRoom.AlreadyExisted
    // ---------------------------------------------------------------

    [Fact]
    public async Task CreateRoom_WhenTheRoomIsNew_DoesNotReportAlreadyExisted()
    {
        var hub = CreateHub("conn-1");

        var response = await hub.CreateRoom(NewRoomName());

        Assert.False(response.AlreadyExisted);
    }

    [Fact]
    public async Task CreateRoom_WhenTheRoomAlreadyExists_ReportsAlreadyExisted()
    {
        // O lobby mostra "Sala ja existe" com base nesta flag.
        var hub = CreateHub("conn-1");
        var room = NewRoomName();

        await hub.CreateRoom(room);
        var second = await hub.CreateRoom(room);

        Assert.True(second.AlreadyExisted);
    }

    // ---------------------------------------------------------------
    // GetPossibleMoves
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetPossibleMoves_ForOwnPiece_ReturnsTheLegalSquares()
    {
        var (room, white, _) = await StartedGame();

        var response = await white.GetPossibleMoves(room, "e2");

        Assert.True(response.Success);
        Assert.Equal("e2", response.From);
        Assert.Equal(
            new[] { "e3", "e4" },
            response.Moves.Select(m => m.Algebraic).OrderBy(s => s, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public async Task GetPossibleMoves_ForAnOpponentPiece_IsRejected()
    {
        // O servidor e a autoridade: um jogador nao enumera as opcoes do adversario.
        var (room, white, _) = await StartedGame();

        var response = await white.GetPossibleMoves(room, "e7");

        Assert.False(response.Success);
        Assert.Empty(response.Moves);
    }

    [Fact]
    public async Task GetPossibleMoves_WhenTheCallerIsNotInTheRoom_IsRejected()
    {
        var (room, _, _) = await StartedGame();
        var intruder = CreateHub("conn-intruder");

        var response = await intruder.GetPossibleMoves(room, "e2");

        Assert.False(response.Success);
        Assert.Empty(response.Moves);
    }

    [Fact]
    public async Task GetPossibleMoves_LeavesTheBoardUntouched()
    {
        var (room, white, _) = await StartedGame();
        var before = FingerprintOf((await white.GetBoardSnapshot(room))!);

        foreach (var square in new[] { "a1", "b1", "c1", "d1", "e1", "g1", "h1", "a2", "e2", "h2" })
            await white.GetPossibleMoves(room, square);

        Assert.Equal(before, FingerprintOf((await white.GetBoardSnapshot(room))!));
    }

    // ---------------------------------------------------------------
    // Acesso concorrente ao tabuleiro da sala
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetPossibleMoves_CalledConcurrentlyByBothPlayers_DoesNotCorruptTheBoard()
    {
        // A avaliacao de legalidade simula o lance no tabuleiro da sala e desfaz.
        // Duas chamadas simultaneas na mesma sala intercalam simulacao e desfazer,
        // e sem serializacao por sala o desfazer de uma apaga a peca da outra.
        var (room, white, black) = await StartedGame();
        var expected = FingerprintOf((await white.GetBoardSnapshot(room))!);

        var whiteSquares = new[] { "a1", "b1", "c1", "d1", "e1", "f1", "g1", "h1", "a2", "b2", "c2", "d2", "e2", "f2", "g2", "h2" };
        var blackSquares = new[] { "a8", "b8", "c8", "d8", "e8", "f8", "g8", "h8", "a7", "b7", "c7", "d7", "e7", "f7", "g7", "h7" };

        for (var round = 0; round < 40; round++)
        {
            var calls = new List<Task>();
            calls.AddRange(whiteSquares.Select(square => white.GetPossibleMoves(room, square)));
            calls.AddRange(blackSquares.Select(square => black.GetPossibleMoves(room, square)));
            await Task.WhenAll(calls);
        }

        Assert.Equal(expected, FingerprintOf((await white.GetBoardSnapshot(room))!));
    }

    [Fact]
    public async Task MakeMove_ConcurrentWithMoveEnumeration_KeepsThirtyTwoPieces()
    {
        var (room, white, black) = await StartedGame();

        for (var round = 0; round < 40; round++)
        {
            await Task.WhenAll(
                white.GetPossibleMoves(room, "b1"),
                black.GetPossibleMoves(room, "b8"),
                white.GetPossibleMoves(room, "e2"),
                black.GetPossibleMoves(room, "e7"));
        }

        // Um lance legal ainda tem de funcionar depois de toda essa concorrencia.
        var moved = await white.MakeMove(room, "e2", "e4");
        Assert.True(moved.Success);

        var snapshot = await white.GetBoardSnapshot(room);
        Assert.Equal(32, snapshot!.Squares.Count(s => s.Piece is not null));
    }

    [Fact]
    public async Task MakeMove_WhenOnePlayerSubmitsTwoMovesAtOnce_OnlyOneIsApplied()
    {
        // A verificacao de turno e a troca de turno estao separadas por um await. Sem
        // serializacao por sala, os dois lances passam pela verificacao antes de
        // qualquer um trocar o turno, e as brancas jogam duas vezes na mesma vez.
        var (room, white, _) = await StartedGame();

        var results = await Task.WhenAll(
            white.MakeMove(room, "e2", "e4"),
            white.MakeMove(room, "d2", "d4"));

        Assert.Equal(1, results.Count(r => r.Success));

        var snapshot = await white.GetBoardSnapshot(room);
        var advanced = snapshot!.Squares.Count(s => s.Algebraic is "e4" or "d4" && s.Piece is not null);
        Assert.Equal(1, advanced);
        Assert.Equal(32, snapshot.Squares.Count(s => s.Piece is not null));
    }

    [Fact]
    public async Task MakeMove_TwoPlayersMovingAtOnce_NeverLosesOrDuplicatesAPiece()
    {
        // Aqui os dois lances podem legitimamente passar, um depois do outro. O que nao
        // pode e o tabuleiro compartilhado terminar inconsistente.
        var (room, white, black) = await StartedGame();

        await Task.WhenAll(
            white.MakeMove(room, "e2", "e4"),
            black.MakeMove(room, "e7", "e5"));

        var snapshot = await white.GetBoardSnapshot(room);
        Assert.Equal(32, snapshot!.Squares.Count(s => s.Piece is not null));
        Assert.Equal(16, snapshot.Squares.Count(s => s.Piece?.Color == "White"));
        Assert.Equal(16, snapshot.Squares.Count(s => s.Piece?.Color == "Black"));
    }
}
