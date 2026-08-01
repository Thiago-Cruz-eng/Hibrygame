using Hibrygame.Enums;
using Orchestrator.Infra.SignalR;
using Xunit;

namespace Orchestrator.Test;

public class GameRoomTests
{
    // ---------------------------------------------------------------
    // IsFull
    // ---------------------------------------------------------------

    [Fact]
    public void IsFull_WhenNoPlayers_ReturnsFalse()
    {
        // Arrange
        var room = new GameRoom("room-empty");

        // Act
        var result = room.IsFull;

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsFull_WhenOnePlayer_ReturnsFalse()
    {
        // Arrange
        var room = new GameRoom("room-one");

        // Act
        room.TryAssignColor("conn-1", "Alice");

        // Assert
        Assert.False(room.IsFull);
    }

    [Fact]
    public void IsFull_WhenTwoPlayers_ReturnsTrue()
    {
        // Arrange
        var room = new GameRoom("room-two");

        // Act
        room.TryAssignColor("conn-1", "Alice");
        room.TryAssignColor("conn-2", "Bob");

        // Assert
        Assert.True(room.IsFull);
    }

    // ---------------------------------------------------------------
    // TryAssignColor
    // ---------------------------------------------------------------

    [Fact]
    public void TryAssignColor_FirstCaller_ReturnsWhite()
    {
        // Arrange
        var room = new GameRoom("room-colors");

        // Act
        var color = room.TryAssignColor("conn-1", "Alice");

        // Assert
        Assert.Equal(ColorEnum.White, color);
    }

    [Fact]
    public void TryAssignColor_SecondCaller_ReturnsBlack()
    {
        // Arrange
        var room = new GameRoom("room-colors-2");
        room.TryAssignColor("conn-1", "Alice");

        // Act
        var color = room.TryAssignColor("conn-2", "Bob");

        // Assert
        Assert.Equal(ColorEnum.Black, color);
    }

    [Fact]
    public void TryAssignColor_ThirdCaller_ReturnsNull()
    {
        // Arrange
        var room = new GameRoom("room-full");
        room.TryAssignColor("conn-1", "Alice");
        room.TryAssignColor("conn-2", "Bob");

        // Act
        var color = room.TryAssignColor("conn-3", "Charlie");

        // Assert
        Assert.Null(color);
    }

    [Fact]
    public void TryAssignColor_SameConnectionIdTwice_ReturnsExistingColor()
    {
        // Arrange
        var room = new GameRoom("room-idempotent");
        var firstCall = room.TryAssignColor("conn-1", "Alice");

        // Act
        var secondCall = room.TryAssignColor("conn-1", "Alice");

        // Assert
        Assert.Equal(firstCall, secondCall);
    }

    [Fact]
    public void TryAssignColor_SameConnectionIdAfterRoomFull_ReturnsExistingColor()
    {
        // Arrange
        var room = new GameRoom("room-idempotent-full");
        room.TryAssignColor("conn-1", "Alice");
        room.TryAssignColor("conn-2", "Bob");

        // Act — conn-1 is already in the room, must get its color back even though room is full
        var result = room.TryAssignColor("conn-1", "Alice");

        // Assert
        Assert.Equal(ColorEnum.White, result);
    }

    // ---------------------------------------------------------------
    // Remove
    // ---------------------------------------------------------------

    [Fact]
    public void Remove_ExistingPlayer_ClearsSlot()
    {
        // Arrange
        var room = new GameRoom("room-remove");
        room.TryAssignColor("conn-1", "Alice");

        // Act
        room.Remove("conn-1");

        // Assert
        Assert.False(room.Players.ContainsKey("conn-1"));
    }

    [Fact]
    public void Remove_ExistingPlayer_RoomIsNoLongerFull()
    {
        // Arrange
        var room = new GameRoom("room-remove-full");
        room.TryAssignColor("conn-1", "Alice");
        room.TryAssignColor("conn-2", "Bob");

        // Act
        room.Remove("conn-1");

        // Assert
        Assert.False(room.IsFull);
    }

    [Fact]
    public void Remove_NonExistentConnectionId_DoesNotThrow()
    {
        // Arrange
        var room = new GameRoom("room-remove-ghost");

        // Act & Assert — should not throw
        var exception = Record.Exception(() => room.Remove("ghost-conn"));
        Assert.Null(exception);
    }

    // ---------------------------------------------------------------
    // Start
    // ---------------------------------------------------------------

    [Fact]
    public void Start_SetsStartedToTrue()
    {
        // Arrange
        var room = new GameRoom("room-start");

        // Act
        room.Start();

        // Assert
        Assert.True(room.Started);
    }

    [Fact]
    public void Start_SetsCurrentTurnToWhite()
    {
        // Arrange
        var room = new GameRoom("room-start-turn");

        // Act
        room.Start();

        // Assert
        Assert.Equal(ColorEnum.White, room.CurrentTurn);
    }

    [Fact]
    public void Start_InitializesBoardWithPositions()
    {
        // Arrange
        var room = new GameRoom("room-start-board");

        // Act
        room.Start();

        // Assert — board should have all 64 positions populated
        var allSquares = room.Board.GetAllSquares();
        Assert.Equal(64, allSquares.Count);
    }

    [Fact]
    public void Start_InitializesBoardWithPieces()
    {
        // Arrange
        var room = new GameRoom("room-start-pieces");

        // Act
        room.Start();

        // Assert — starting position has 32 pieces (16 per side)
        var piecedSquares = room.Board.GetPositionsPlaced();
        Assert.Equal(32, piecedSquares.Count);
    }

    [Fact]
    public void Start_IsIdempotent_CallingTwiceDoesNotDoublePieces()
    {
        // Arrange
        var room = new GameRoom("room-start-idempotent");
        room.Start();

        // Act
        room.Start();

        // Assert — piece count should still be 32, not 64
        var piecedSquares = room.Board.GetPositionsPlaced();
        Assert.Equal(32, piecedSquares.Count);
    }

    [Fact]
    public void Start_IsIdempotent_StartedRemainsTrue()
    {
        // Arrange
        var room = new GameRoom("room-start-idempotent-flag");
        room.Start();

        // Act
        room.Start();

        // Assert
        Assert.True(room.Started);
    }

    // ---------------------------------------------------------------
    // SwitchTurn
    // ---------------------------------------------------------------

    [Fact]
    public void SwitchTurn_WhenWhite_TogglestoBlack()
    {
        // Arrange
        var room = new GameRoom("room-switch-1");
        room.Start(); // sets CurrentTurn = White

        // Act
        room.SwitchTurn();

        // Assert
        Assert.Equal(ColorEnum.Black, room.CurrentTurn);
    }

    [Fact]
    public void SwitchTurn_WhenBlack_TogglestoWhite()
    {
        // Arrange
        var room = new GameRoom("room-switch-2");
        room.Start();
        room.SwitchTurn(); // now Black

        // Act
        room.SwitchTurn();

        // Assert
        Assert.Equal(ColorEnum.White, room.CurrentTurn);
    }

    // ---------------------------------------------------------------
    // Finish
    // ---------------------------------------------------------------

    [Fact]
    public void Finish_SetsFinishedToTrue()
    {
        // Arrange
        var room = new GameRoom("room-finish");

        // Act
        room.Finish();

        // Assert
        Assert.True(room.Finished);
    }

    [Fact]
    public void Finish_CanBeCalledMultipleTimes_StaysTrue()
    {
        // Arrange
        var room = new GameRoom("room-finish-twice");
        room.Finish();

        // Act
        room.Finish();

        // Assert
        Assert.True(room.Finished);
    }
}
