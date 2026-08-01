using Microsoft.AspNetCore.SignalR;
using Moq;
using Orchestrator.Infra.SignalR;
using Xunit;

namespace Orchestrator.Test;

/// <summary>
/// Smoke test de partida completa (Fase 5 do refactor).
///
/// Joga o Mate do Pastor do inicio ao fim atravessando o caminho real que o frontend
/// usa: ChessHub -> motor de regras -> snapshot. Cobre abertura, avanco duplo de peao,
/// movimento de cavalo, bispo e dama, captura, xeque, alternancia de turno e rejeicao de
/// jogada fora de turno e de jogada ilegal.
///
/// Nao precisa de MongoDB: o hub nao toca em persistencia.
///
/// Nao cobre roque nem fim de jogo porque nenhum dos dois existe no motor — sao ausencia
/// de funcionalidade, registrada no relatorio, e nao regressao.
/// </summary>
public class FullGameSmokeTests
{
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

        return new ChessHub { Clients = clients.Object, Groups = groups.Object, Context = context.Object };
    }

    private static ChessHub.SquareDto Square(ChessHub.BoardSnapshot snapshot, string algebraic) =>
        snapshot.Squares.Single(s => s.Algebraic == algebraic);

    [Fact]
    public async Task ScholarsMateLine_PlaysEndToEndThroughTheHub()
    {
        // Arrange — sala com dois jogadores e partida iniciada.
        var room = $"smoke-{Guid.NewGuid()}";
        var white = CreateHub("conn-white");
        var black = CreateHub("conn-black");

        await white.CreateRoom(room);
        var whiteJoin = await white.JoinRoom("Branca", room);
        var blackJoin = await black.JoinRoom("Preta", room);
        var started = await white.StartGame(room);

        Assert.Equal("White", whiteJoin.Color);
        Assert.Equal("Black", blackJoin.Color);
        Assert.True(started.Success);
        Assert.Equal(32, started.Snapshot!.Squares.Count(s => s.Piece is not null));
        Assert.Equal("White", started.Snapshot.CurrentTurn);

        // As pretas nao podem abrir: nao e a vez delas.
        var offTurn = await black.MakeMove(room, "e7", "e5");
        Assert.False(offTurn.Success);
        Assert.Equal("Not your turn.", offTurn.Message);

        // 1. e4 e5
        var move = await white.MakeMove(room, "e2", "e4");
        Assert.True(move.Success);
        Assert.Equal("Black", move.NextTurn);
        Assert.Null(Square(move.Snapshot!, "e2").Piece);
        Assert.Equal("Pawn", Square(move.Snapshot!, "e4").Piece!.Type);

        move = await black.MakeMove(room, "e7", "e5");
        Assert.True(move.Success);
        Assert.Equal("White", move.NextTurn);

        // 2. Qh5 Nc6 — dama sai pela diagonal aberta pelo peao.
        move = await white.MakeMove(room, "d1", "h5");
        Assert.True(move.Success);
        Assert.Equal("Queen", Square(move.Snapshot!, "h5").Piece!.Type);

        move = await black.MakeMove(room, "b8", "c6");
        Assert.True(move.Success);
        Assert.Equal("Knight", Square(move.Snapshot!, "c6").Piece!.Type);

        // 3. Bc4 Nf6
        move = await white.MakeMove(room, "f1", "c4");
        Assert.True(move.Success);
        Assert.Equal("Bishop", Square(move.Snapshot!, "c4").Piece!.Type);

        move = await black.MakeMove(room, "g8", "f6");
        Assert.True(move.Success);

        // 4. Qxf7+ — captura e xeque na mesma jogada.
        var beforeCapture = (await white.GetBoardSnapshot(room))!;
        Assert.Equal(32, beforeCapture.Squares.Count(s => s.Piece is not null));
        Assert.Equal("Black", Square(beforeCapture, "f7").Piece!.Color);

        move = await white.MakeMove(room, "h5", "f7");
        Assert.True(move.Success);

        var afterCapture = move.Snapshot!;
        Assert.Equal(31, afterCapture.Squares.Count(s => s.Piece is not null));
        Assert.Equal("Queen", Square(afterCapture, "f7").Piece!.Type);
        Assert.Equal("White", Square(afterCapture, "f7").Piece!.Color);
        Assert.Null(Square(afterCapture, "h5").Piece);

        // O rei preto esta em xeque, e o snapshot leva essa informacao ao frontend.
        Assert.True(Square(afterCapture, "e8").Piece!.IsInCheckState);

        // Em xeque, as pretas nao podem simplesmente jogar outra coisa.
        var ignoringCheck = await black.MakeMove(room, "a7", "a6");
        Assert.False(ignoringCheck.Success);

        var stillInCheck = (await black.GetBoardSnapshot(room))!;
        Assert.Equal("Black", stillInCheck.CurrentTurn);
        Assert.Equal(31, stillInCheck.Squares.Count(s => s.Piece is not null));
        Assert.Equal("Pawn", Square(stillInCheck, "a7").Piece!.Type);
    }

    [Fact]
    public async Task Opening_RejectsMovingAPieceThatIsNotYours()
    {
        var room = $"smoke-{Guid.NewGuid()}";
        var white = CreateHub("conn-white");
        var black = CreateHub("conn-black");

        await white.CreateRoom(room);
        await white.JoinRoom("Branca", room);
        await black.JoinRoom("Preta", room);
        await white.StartGame(room);

        var response = await white.MakeMove(room, "e7", "e5");

        Assert.False(response.Success);
        Assert.Equal("That piece is not yours.", response.Message);
    }

    [Fact]
    public async Task Opening_RejectsAGeometricallyImpossibleMove()
    {
        var room = $"smoke-{Guid.NewGuid()}";
        var white = CreateHub("conn-white");
        var black = CreateHub("conn-black");

        await white.CreateRoom(room);
        await white.JoinRoom("Branca", room);
        await black.JoinRoom("Preta", room);
        await white.StartGame(room);

        // Torre em a1 nao vai a a5: a2 esta ocupada pelo proprio peao.
        var response = await white.MakeMove(room, "a1", "a5");

        Assert.False(response.Success);
        Assert.Equal("Illegal move.", response.Message);
    }
}
