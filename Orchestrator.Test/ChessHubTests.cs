using Microsoft.AspNetCore.SignalR;
using Moq;
using Orchestrator.Infra.SignalR;
using Xunit;

namespace Orchestrator.Test;

public class ChessHubTests
{
    [Fact]
    public async Task JoinRoom_PlayerJoined_CorrectMessageSent()
    {
        // Arrange
        var hub = new ChessHub();
        var mockClients = new Mock<IHubClients>();
        var mockGroups = new Mock<IGroupManager>();

        hub.Clients = mockClients.Object;
        hub.Groups = mockGroups.Object;

        var connectionId = "connectionId";
        var playerName = "Player1";

        // Act
        await hub.JoinRoom(playerName);

        // Assert
        mockGroups.Verify(m => m.AddToGroupAsync(connectionId, It.IsAny<string>()), Times.Once);
        mockClients.Group(It.IsAny<string>()).Verify(m => m.SendAsync("PlayerJoined", playerName), Times.Once);
    }

    [Fact]
    public async Task SendMove_ValidMove_CorrectMessageSent()
    {
        // Arrange
        var hub = new ChessHub();
        var mockClients = new Mock<IHubClients>();
        var mockGroups = new Mock<IGroupManager>();

        hub.Clients = mockClients.Object;
        hub.Groups = mockGroups.Object;

        var connectionId = "connectionId";
        var playerName = "Player1";
        var roomName = "Room1";
        var move = "e2-e4";

        await hub.JoinRoom(playerName);

        // Act
        await hub.SendMove(move);

        // Assert
        mockClients.Group(roomName).Verify(m => m.SendAsync("ReceiveMove", move), Times.Once);
    }
    
}