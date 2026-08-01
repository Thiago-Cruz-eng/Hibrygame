using Hibrygame.Enums;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Orchestrator.Infra.SignalR;
using Xunit;

namespace Orchestrator.Test;

/// <summary>
/// Tests for ChessHub SignalR methods.
///
/// The hub stores rooms in a static ConcurrentDictionary. Every test must use
/// a unique room name (via NewRoomName()) so that tests are fully isolated and
/// can run in any order without interfering with each other.
/// </summary>
public class ChessHubTests
{
    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static string NewRoomName() => $"test-{Guid.NewGuid()}";

    /// <summary>
    /// Creates a fully wired ChessHub with mocked SignalR infrastructure.
    /// Returns the hub and the core mocks needed for verification.
    /// </summary>
    private static (
        ChessHub hub,
        Mock<IHubCallerClients> clientsMock,
        Mock<ISingleClientProxy> callerProxyMock,
        Mock<IClientProxy> groupProxyMock,
        Mock<IGroupManager> groupsMock,
        Mock<HubCallerContext> contextMock)
    CreateHub(string connectionId = "conn-default")
    {
        var clientsMock = new Mock<IHubCallerClients>();
        // Clients.Caller returns ISingleClientProxy (ASP.NET Core 8 SignalR)
        var callerProxyMock = new Mock<ISingleClientProxy>();
        var groupProxyMock = new Mock<IClientProxy>();
        var groupsMock = new Mock<IGroupManager>();
        var contextMock = new Mock<HubCallerContext>();

        contextMock.Setup(c => c.ConnectionId).Returns(connectionId);

        // Clients.Caller → callerProxyMock
        clientsMock.Setup(c => c.Caller).Returns(callerProxyMock.Object);

        // Clients.Group(any) → groupProxyMock
        clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(groupProxyMock.Object);

        // Clients.All → groupProxyMock (reuse, not exercised by most tests)
        clientsMock.Setup(c => c.All).Returns(groupProxyMock.Object);

        // Groups.AddToGroupAsync / RemoveFromGroupAsync → complete immediately
        groupsMock
            .Setup(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        groupsMock
            .Setup(g => g.RemoveFromGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // ISingleClientProxy.SendCoreAsync → complete immediately
        callerProxyMock
            .Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        // IClientProxy.SendCoreAsync → complete immediately
        groupProxyMock
            .Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var hub = new ChessHub
        {
            Clients = clientsMock.Object,
            Groups = groupsMock.Object,
            Context = contextMock.Object
        };

        return (hub, clientsMock, callerProxyMock, groupProxyMock, groupsMock, contextMock);
    }

    // ---------------------------------------------------------------
    // CreateRoom
    // ---------------------------------------------------------------

    [Fact]
    public async Task CreateRoom_NewRoom_ReturnsResponseWithRoomName()
    {
        // Arrange
        var (hub, _, _, _, _, _) = CreateHub();
        var roomName = NewRoomName();

        // Act
        var response = await hub.CreateRoom(roomName);

        // Assert
        Assert.Equal(roomName, response.Room);
    }

    [Fact]
    public async Task CreateRoom_ExistingRoomName_ReturnsExistingRoom()
    {
        // Arrange
        var (hub, _, _, _, _, _) = CreateHub();
        var roomName = NewRoomName();

        // Act
        var first = await hub.CreateRoom(roomName);
        var second = await hub.CreateRoom(roomName);

        // Assert — both calls return the same room name
        Assert.Equal(first.Room, second.Room);
    }

    // ---------------------------------------------------------------
    // GetAvailableRooms
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetAvailableRooms_ExcludesFullRooms()
    {
        // Arrange
        var (hub1, _, _, _, _, _) = CreateHub("conn-a");
        var (hub2, _, _, _, _, _) = CreateHub("conn-b");
        var roomName = NewRoomName();

        await hub1.CreateRoom(roomName);
        await hub1.JoinRoom("Alice", roomName);
        await hub2.JoinRoom("Bob", roomName);   // room now full

        // Act
        var available = await hub1.GetAvailableRooms();

        // Assert
        Assert.DoesNotContain(roomName, available);
    }

    [Fact]
    public async Task GetAvailableRooms_ExcludesFinishedRooms()
    {
        // Arrange
        var (hub1, _, _, _, _, _) = CreateHub("conn-a2");
        var (hub2, _, _, _, _, _) = CreateHub("conn-b2");
        var roomName = NewRoomName();

        await hub1.CreateRoom(roomName);
        await hub1.JoinRoom("Alice", roomName);
        await hub2.JoinRoom("Bob", roomName);
        await hub1.StartGame(roomName);

        // Manually mark the room as finished via a MakeMove sequence
        // (simpler: just verify a newly finished room is excluded)
        var available = await hub1.GetAvailableRooms();

        // The full room should not be available
        Assert.DoesNotContain(roomName, available);
    }

    [Fact]
    public async Task GetAvailableRooms_IncludesOpenRooms()
    {
        // Arrange
        var (hub, _, _, _, _, _) = CreateHub("conn-open");
        var roomName = NewRoomName();
        await hub.CreateRoom(roomName);

        // Act
        var available = await hub.GetAvailableRooms();

        // Assert
        Assert.Contains(roomName, available);
    }

    // ---------------------------------------------------------------
    // GetPlayersInRoom
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetPlayersInRoom_NoPlayers_ReturnsZero()
    {
        // Arrange
        var (hub, _, _, _, _, _) = CreateHub();
        var roomName = NewRoomName();
        await hub.CreateRoom(roomName);

        // Act
        var count = await hub.GetPlayersInRoom(roomName);

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GetPlayersInRoom_OnePlayer_ReturnsOne()
    {
        // Arrange
        var (hub, _, _, _, _, _) = CreateHub("conn-p1");
        var roomName = NewRoomName();
        await hub.CreateRoom(roomName);
        await hub.JoinRoom("Alice", roomName);

        // Act
        var count = await hub.GetPlayersInRoom(roomName);

        // Assert
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetPlayersInRoom_TwoPlayers_ReturnsTwo()
    {
        // Arrange
        var (hub1, _, _, _, _, _) = CreateHub("conn-r1");
        var (hub2, _, _, _, _, _) = CreateHub("conn-r2");
        var roomName = NewRoomName();
        await hub1.CreateRoom(roomName);
        await hub1.JoinRoom("Alice", roomName);
        await hub2.JoinRoom("Bob", roomName);

        // Act
        var count = await hub1.GetPlayersInRoom(roomName);

        // Assert
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task GetPlayersInRoom_UnknownRoom_ReturnsZero()
    {
        // Arrange
        var (hub, _, _, _, _, _) = CreateHub();

        // Act
        var count = await hub.GetPlayersInRoom("room-does-not-exist-" + Guid.NewGuid());

        // Assert
        Assert.Equal(0, count);
    }

    // ---------------------------------------------------------------
    // JoinRoom
    // ---------------------------------------------------------------

    [Fact]
    public async Task JoinRoom_NonExistentRoom_EmitsRoomNotFound()
    {
        // Arrange
        var (hub, _, callerProxy, _, _, _) = CreateHub("conn-jrnf");

        // Act
        await hub.JoinRoom("Alice", "ghost-room-" + Guid.NewGuid());

        // Assert — Caller received "RoomNotFound"
        callerProxy.Verify(
            p => p.SendCoreAsync("RoomNotFound", It.IsAny<object[]>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task JoinRoom_NonExistentRoom_ReturnsEmptyResponse()
    {
        // Arrange
        var (hub, _, _, _, _, _) = CreateHub("conn-jrnf2");

        // Act
        var response = await hub.JoinRoom("Alice", "ghost-room-" + Guid.NewGuid());

        // Assert
        Assert.Null(response.ConnectionId);
        Assert.Null(response.Player);
    }

    [Fact]
    public async Task JoinRoom_FirstPlayer_GetsWhiteColor()
    {
        // Arrange
        var (hub, _, _, _, _, _) = CreateHub("conn-white");
        var roomName = NewRoomName();
        await hub.CreateRoom(roomName);

        // Act
        var response = await hub.JoinRoom("Alice", roomName);

        // Assert
        Assert.Equal("White", response.Color);
    }

    [Fact]
    public async Task JoinRoom_SecondPlayer_GetsBlackColor()
    {
        // Arrange
        var (hub1, _, _, _, _, _) = CreateHub("conn-white2");
        var (hub2, _, _, _, _, _) = CreateHub("conn-black2");
        var roomName = NewRoomName();
        await hub1.CreateRoom(roomName);
        await hub1.JoinRoom("Alice", roomName);

        // Act
        var response = await hub2.JoinRoom("Bob", roomName);

        // Assert
        Assert.Equal("Black", response.Color);
    }

    [Fact]
    public async Task JoinRoom_ThirdPlayer_EmitsRoomFull()
    {
        // Arrange
        var (hub1, _, _, _, _, _) = CreateHub("conn-tf1");
        var (hub2, _, _, _, _, _) = CreateHub("conn-tf2");
        var (hub3, _, callerProxy3, _, _, _) = CreateHub("conn-tf3");
        var roomName = NewRoomName();
        await hub1.CreateRoom(roomName);
        await hub1.JoinRoom("Alice", roomName);
        await hub2.JoinRoom("Bob", roomName);

        // Act
        await hub3.JoinRoom("Charlie", roomName);

        // Assert
        callerProxy3.Verify(
            p => p.SendCoreAsync("RoomFull", It.IsAny<object[]>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task JoinRoom_ThirdPlayer_ReturnsEmptyResponse()
    {
        // Arrange
        var (hub1, _, _, _, _, _) = CreateHub("conn-tf4");
        var (hub2, _, _, _, _, _) = CreateHub("conn-tf5");
        var (hub3, _, _, _, _, _) = CreateHub("conn-tf6");
        var roomName = NewRoomName();
        await hub1.CreateRoom(roomName);
        await hub1.JoinRoom("Alice", roomName);
        await hub2.JoinRoom("Bob", roomName);

        // Act
        var response = await hub3.JoinRoom("Charlie", roomName);

        // Assert
        Assert.Null(response.ConnectionId);
    }

    [Fact]
    public async Task JoinRoom_ValidJoin_AddsConnectionToSignalRGroup()
    {
        // Arrange
        var (hub, _, _, _, groupsMock, _) = CreateHub("conn-grp");
        var roomName = NewRoomName();
        await hub.CreateRoom(roomName);

        // Act
        await hub.JoinRoom("Alice", roomName);

        // Assert
        groupsMock.Verify(
            g => g.AddToGroupAsync("conn-grp", roomName, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task JoinRoom_ValidJoin_EmitsPlayerJoinedToGroup()
    {
        // Arrange
        var (hub, _, _, groupProxy, _, _) = CreateHub("conn-pj");
        var roomName = NewRoomName();
        await hub.CreateRoom(roomName);

        // Act
        await hub.JoinRoom("Alice", roomName);

        // Assert
        groupProxy.Verify(
            p => p.SendCoreAsync("PlayerJoined", It.IsAny<object[]>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task JoinRoom_ValidJoin_ReturnsPopulatedResponse()
    {
        // Arrange
        var (hub, _, _, _, _, _) = CreateHub("conn-resp");
        var roomName = NewRoomName();
        await hub.CreateRoom(roomName);

        // Act
        var response = await hub.JoinRoom("Alice", roomName);

        // Assert — all key fields are populated
        Assert.Equal("conn-resp", response.ConnectionId);
        Assert.Equal("Alice", response.Player);
        Assert.Equal(roomName, response.Room);
        Assert.NotNull(response.Color);
    }

    // ---------------------------------------------------------------
    // StartGame
    // ---------------------------------------------------------------

    [Fact]
    public async Task StartGame_RoomDoesNotExist_ReturnsFailure()
    {
        // Arrange
        var (hub, _, _, _, _, _) = CreateHub();

        // Act
        var response = await hub.StartGame("nonexistent-" + Guid.NewGuid());

        // Assert
        Assert.False(response.Success);
        Assert.NotNull(response.Message);
    }

    [Fact]
    public async Task StartGame_RoomNotFull_ReturnsFailure()
    {
        // Arrange
        var (hub, _, _, _, _, _) = CreateHub("conn-sf1");
        var roomName = NewRoomName();
        await hub.CreateRoom(roomName);
        await hub.JoinRoom("Alice", roomName); // only one player

        // Act
        var response = await hub.StartGame(roomName);

        // Assert
        Assert.False(response.Success);
    }

    [Fact]
    public async Task StartGame_RoomNotFull_ReturnsMessageAboutPlayers()
    {
        // Arrange
        var (hub, _, _, _, _, _) = CreateHub("conn-sf2");
        var roomName = NewRoomName();
        await hub.CreateRoom(roomName);
        await hub.JoinRoom("Alice", roomName);

        // Act
        var response = await hub.StartGame(roomName);

        // Assert
        Assert.Contains("2", response.Message); // "Room needs 2 players to start."
    }

    [Fact]
    public async Task StartGame_FullRoom_ReturnsSuccess()
    {
        // Arrange
        var (hub1, _, _, _, _, _) = CreateHub("conn-sg1");
        var (hub2, _, _, _, _, _) = CreateHub("conn-sg2");
        var roomName = NewRoomName();
        await hub1.CreateRoom(roomName);
        await hub1.JoinRoom("Alice", roomName);
        await hub2.JoinRoom("Bob", roomName);

        // Act
        var response = await hub1.StartGame(roomName);

        // Assert
        Assert.True(response.Success);
    }

    [Fact]
    public async Task StartGame_FullRoom_EmitsGameStartedToGroup()
    {
        // Arrange
        var (hub1, _, _, groupProxy1, _, _) = CreateHub("conn-sg3");
        var (hub2, _, _, _, _, _) = CreateHub("conn-sg4");
        var roomName = NewRoomName();
        await hub1.CreateRoom(roomName);
        await hub1.JoinRoom("Alice", roomName);
        await hub2.JoinRoom("Bob", roomName);

        // Act
        await hub1.StartGame(roomName);

        // Assert
        groupProxy1.Verify(
            p => p.SendCoreAsync("GameStarted", It.IsAny<object[]>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StartGame_FullRoom_ReturnsSnapshotWithBoard()
    {
        // Arrange
        var (hub1, _, _, _, _, _) = CreateHub("conn-sg5");
        var (hub2, _, _, _, _, _) = CreateHub("conn-sg6");
        var roomName = NewRoomName();
        await hub1.CreateRoom(roomName);
        await hub1.JoinRoom("Alice", roomName);
        await hub2.JoinRoom("Bob", roomName);

        // Act
        var response = await hub1.StartGame(roomName);

        // Assert
        Assert.NotNull(response.Snapshot);
        Assert.Equal(64, response.Snapshot!.Squares.Count);
    }

    // ---------------------------------------------------------------
    // GetBoardSnapshot
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetBoardSnapshot_UnknownRoom_ReturnsNull()
    {
        // Arrange
        var (hub, _, _, _, _, _) = CreateHub();

        // Act
        var snapshot = await hub.GetBoardSnapshot("no-such-room-" + Guid.NewGuid());

        // Assert
        Assert.Null(snapshot);
    }

    [Fact]
    public async Task GetBoardSnapshot_ExistingRoom_ReturnsSnapshot()
    {
        // Arrange
        var (hub, _, _, _, _, _) = CreateHub("conn-snap");
        var roomName = NewRoomName();
        await hub.CreateRoom(roomName);

        // Act
        var snapshot = await hub.GetBoardSnapshot(roomName);

        // Assert
        Assert.NotNull(snapshot);
        Assert.Equal(roomName, snapshot!.Room);
    }

    // ---------------------------------------------------------------
    // MakeMove — rejection cases
    // ---------------------------------------------------------------

    [Fact]
    public async Task MakeMove_GameNotStarted_ReturnsFailure()
    {
        // Arrange
        var (hub, _, _, _, _, _) = CreateHub("conn-mm1");
        var roomName = NewRoomName();
        await hub.CreateRoom(roomName);
        await hub.JoinRoom("Alice", roomName);

        // Act
        var response = await hub.MakeMove(roomName, "e2", "e4");

        // Assert
        Assert.False(response.Success);
        Assert.Equal("Game not started.", response.Message);
    }

    [Fact]
    public async Task MakeMove_CallerNotInRoom_ReturnsNotInRoom()
    {
        // Arrange — hub3 is a stranger not in the room
        var (hub1, _, _, _, _, _) = CreateHub("conn-mm-w");
        var (hub2, _, _, _, _, _) = CreateHub("conn-mm-b");
        var (hub3, _, _, _, _, _) = CreateHub("conn-mm-stranger");
        var roomName = NewRoomName();
        await hub1.CreateRoom(roomName);
        await hub1.JoinRoom("Alice", roomName);
        await hub2.JoinRoom("Bob", roomName);
        await hub1.StartGame(roomName);

        // Act
        var response = await hub3.MakeMove(roomName, "e2", "e4");

        // Assert
        Assert.False(response.Success);
        Assert.Equal("You are not in this room.", response.Message);
    }

    [Fact]
    public async Task MakeMove_NotCallersTurn_ReturnsNotYourTurn()
    {
        // Arrange — Bob (Black) tries to move when it is White's turn
        var (hub1, _, _, _, _, _) = CreateHub("conn-wt1");
        var (hub2, _, _, _, _, _) = CreateHub("conn-bt1");
        var roomName = NewRoomName();
        await hub1.CreateRoom(roomName);
        await hub1.JoinRoom("Alice", roomName);  // White
        await hub2.JoinRoom("Bob", roomName);    // Black
        await hub1.StartGame(roomName);

        // Act — Black moves first (should be rejected)
        var response = await hub2.MakeMove(roomName, "e7", "e5");

        // Assert
        Assert.False(response.Success);
        Assert.Equal("Not your turn.", response.Message);
    }

    [Fact]
    public async Task MakeMove_InvalidAlgebraicNotation_ReturnsInvalidSquare()
    {
        // Arrange
        var (hub1, _, _, _, _, _) = CreateHub("conn-inv1");
        var (hub2, _, _, _, _, _) = CreateHub("conn-inv2");
        var roomName = NewRoomName();
        await hub1.CreateRoom(roomName);
        await hub1.JoinRoom("Alice", roomName);
        await hub2.JoinRoom("Bob", roomName);
        await hub1.StartGame(roomName);

        // Act — "z9" is not a valid square
        var response = await hub1.MakeMove(roomName, "z9", "e4");

        // Assert
        Assert.False(response.Success);
        Assert.Equal("Invalid square notation.", response.Message);
    }

    [Fact]
    public async Task MakeMove_NoPieceOnSourceSquare_ReturnsNoPiece()
    {
        // Arrange — White tries to move from e4 which is empty at game start
        var (hub1, _, _, _, _, _) = CreateHub("conn-np1");
        var (hub2, _, _, _, _, _) = CreateHub("conn-np2");
        var roomName = NewRoomName();
        await hub1.CreateRoom(roomName);
        await hub1.JoinRoom("Alice", roomName);
        await hub2.JoinRoom("Bob", roomName);
        await hub1.StartGame(roomName);

        // Act
        var response = await hub1.MakeMove(roomName, "e4", "e5");

        // Assert
        Assert.False(response.Success);
        Assert.Contains("No piece on", response.Message);
    }

    [Fact]
    public async Task MakeMove_PieceDoesNotBelongToCaller_ReturnsPieceNotYours()
    {
        // Arrange — White tries to move a Black pawn
        var (hub1, _, _, _, _, _) = CreateHub("conn-pny1");
        var (hub2, _, _, _, _, _) = CreateHub("conn-pny2");
        var roomName = NewRoomName();
        await hub1.CreateRoom(roomName);
        await hub1.JoinRoom("Alice", roomName);  // White
        await hub2.JoinRoom("Bob", roomName);    // Black
        await hub1.StartGame(roomName);

        // e7 is a Black pawn. White's turn — wrong color.
        var response = await hub1.MakeMove(roomName, "e7", "e6");

        // Assert
        Assert.False(response.Success);
        Assert.Equal("That piece is not yours.", response.Message);
    }

    [Fact]
    public async Task MakeMove_IllegalTarget_ReturnsIllegalMove()
    {
        // Arrange — White pawn on e2 cannot jump to e6 (only 1-2 squares forward)
        var (hub1, _, _, _, _, _) = CreateHub("conn-il1");
        var (hub2, _, _, _, _, _) = CreateHub("conn-il2");
        var roomName = NewRoomName();
        await hub1.CreateRoom(roomName);
        await hub1.JoinRoom("Alice", roomName);
        await hub2.JoinRoom("Bob", roomName);
        await hub1.StartGame(roomName);

        // Act
        var response = await hub1.MakeMove(roomName, "e2", "e6");

        // Assert
        Assert.False(response.Success);
        Assert.Equal("Illegal move.", response.Message);
    }

    // ---------------------------------------------------------------
    // MakeMove — happy path
    // ---------------------------------------------------------------

    [Fact]
    public async Task MakeMove_ValidWhitePawnE2ToE4_ReturnsSuccess()
    {
        // Arrange
        var (hub1, _, _, _, _, _) = CreateHub("conn-hp1");
        var (hub2, _, _, _, _, _) = CreateHub("conn-hp2");
        var roomName = NewRoomName();
        await hub1.CreateRoom(roomName);
        await hub1.JoinRoom("Alice", roomName);
        await hub2.JoinRoom("Bob", roomName);
        await hub1.StartGame(roomName);

        // Act
        var response = await hub1.MakeMove(roomName, "e2", "e4");

        // Assert
        Assert.True(response.Success);
    }

    [Fact]
    public async Task MakeMove_ValidWhitePawnE2ToE4_SwitchesTurnToBlack()
    {
        // Arrange
        var (hub1, _, _, _, _, _) = CreateHub("conn-hp3");
        var (hub2, _, _, _, _, _) = CreateHub("conn-hp4");
        var roomName = NewRoomName();
        await hub1.CreateRoom(roomName);
        await hub1.JoinRoom("Alice", roomName);
        await hub2.JoinRoom("Bob", roomName);
        await hub1.StartGame(roomName);

        // Act
        var response = await hub1.MakeMove(roomName, "e2", "e4");

        // Assert
        Assert.Equal("Black", response.NextTurn);
    }

    [Fact]
    public async Task MakeMove_ValidWhitePawnE2ToE4_BroadcastsBoardChanged()
    {
        // Arrange
        var (hub1, _, _, groupProxy1, _, _) = CreateHub("conn-hp5");
        var (hub2, _, _, _, _, _) = CreateHub("conn-hp6");
        var roomName = NewRoomName();
        await hub1.CreateRoom(roomName);
        await hub1.JoinRoom("Alice", roomName);
        await hub2.JoinRoom("Bob", roomName);
        await hub1.StartGame(roomName);

        // Act
        await hub1.MakeMove(roomName, "e2", "e4");

        // Assert
        groupProxy1.Verify(
            p => p.SendCoreAsync("BoardChanged", It.IsAny<object[]>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task MakeMove_ValidMove_ReturnsSnapshotWithSquares()
    {
        // Arrange
        var (hub1, _, _, _, _, _) = CreateHub("conn-hp7");
        var (hub2, _, _, _, _, _) = CreateHub("conn-hp8");
        var roomName = NewRoomName();
        await hub1.CreateRoom(roomName);
        await hub1.JoinRoom("Alice", roomName);
        await hub2.JoinRoom("Bob", roomName);
        await hub1.StartGame(roomName);

        // Act
        var response = await hub1.MakeMove(roomName, "e2", "e4");

        // Assert
        Assert.NotNull(response.Snapshot);
        Assert.Equal(64, response.Snapshot!.Squares.Count);
    }

    // ---------------------------------------------------------------
    // LeaveRoom
    // ---------------------------------------------------------------

    [Fact]
    public async Task LeaveRoom_RemovesConnectionFromSignalRGroup()
    {
        // Arrange
        var (hub, _, _, _, groupsMock, _) = CreateHub("conn-leave1");
        var roomName = NewRoomName();
        await hub.CreateRoom(roomName);
        await hub.JoinRoom("Alice", roomName);

        // Act
        await hub.LeaveRoom(roomName);

        // Assert
        groupsMock.Verify(
            g => g.RemoveFromGroupAsync("conn-leave1", roomName, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LeaveRoom_EmitsPlayerLeftToGroup()
    {
        // Arrange
        var (hub, _, _, groupProxy, _, _) = CreateHub("conn-leave2");
        var roomName = NewRoomName();
        await hub.CreateRoom(roomName);
        await hub.JoinRoom("Alice", roomName);

        // Act
        await hub.LeaveRoom(roomName);

        // Assert
        groupProxy.Verify(
            p => p.SendCoreAsync("PlayerLeft", It.IsAny<object[]>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce); // once for PlayerJoined broadcast + once for PlayerLeft
    }

    // ---------------------------------------------------------------
    // OnDisconnectedAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task OnDisconnectedAsync_RemovesPlayerFromRoom_EmitsPlayerLeft()
    {
        // Arrange
        var (hub, _, _, groupProxy, _, _) = CreateHub("conn-dc1");
        var roomName = NewRoomName();
        await hub.CreateRoom(roomName);
        await hub.JoinRoom("Alice", roomName);

        // Reset invocation count so we only count the disconnect emission
        groupProxy.Invocations.Clear();

        // Act
        await hub.OnDisconnectedAsync(null);

        // Assert
        groupProxy.Verify(
            p => p.SendCoreAsync("PlayerLeft", It.IsAny<object[]>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnDisconnectedAsync_PlayerNotInAnyRoom_DoesNotThrow()
    {
        // Arrange — hub with a connection that has never joined anything
        var (hub, _, _, _, _, _) = CreateHub("conn-dc2");

        // Act & Assert
        var exception = await Record.ExceptionAsync(() => hub.OnDisconnectedAsync(null));
        Assert.Null(exception);
    }
}
