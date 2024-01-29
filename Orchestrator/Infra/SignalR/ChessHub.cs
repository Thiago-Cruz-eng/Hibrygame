using Hibrygame.Enums;
using Microsoft.AspNetCore.SignalR;

namespace Orchestrator.Infra.SignalR;

public class ChessHub : Hub
{
    private static Dictionary<string, string> playerRooms = new ();

    public async Task JoinRoom(string playerName)
    {
        var room = GetAvailableRoom();
        await Groups.AddToGroupAsync(Context.ConnectionId, room);
        playerRooms[Context.ConnectionId] = room;

        await Clients.Group(room).SendAsync("PlayerJoined", playerName);

        if (IsRoomFull(room))
        {
            await Clients.Group(room).SendAsync("StartGame");
        }
    }

    public async Task SendMove(string move)
    {
        string room = playerRooms[Context.ConnectionId];
        await Clients.OthersInGroup(room).SendAsync("ReceiveMove", move);
    }

    private string GetAvailableRoom()
    {
        foreach (var room in playerRooms.Values.Distinct())
        {
            if (playerRooms.Count(p => p.Value == room) < 2)
            {
                return room;
            }
        }
        return "Room" + playerRooms.Count / 2;
    }

    private bool IsRoomFull(string room)
    {
        return playerRooms.Count(p => p.Value == room) == 2;
    }
}