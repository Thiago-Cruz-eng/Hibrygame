using Hibrygame;
using Hibrygame.Enums;
using Microsoft.AspNetCore.SignalR;

namespace Orchestrator.Infra.SignalR
{
    public class ChessHub : Hub
    {
        private static Dictionary<string, Board> games = new Dictionary<string, Board>();
        private static Dictionary<string, string> playerRooms = new Dictionary<string, string>();

        public async Task JoinRoom(string playerName)
        {
            var room = GetAvailableRoom();
            await Groups.AddToGroupAsync(Context.ConnectionId, room);
            playerRooms[Context.ConnectionId] = room;

            if (!games.ContainsKey(room))
            {
                games[room] = new Board();
            }

            await Clients.Group(room).SendAsync("PlayerJoined", playerName);

            if (IsRoomFull(room))
            {
                games.Values.FirstOrDefault().StartBoard();
                await Clients.Group(room).SendAsync("StartGame");
            }
        }

        public async Task LeaveRoom(string roomName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomName);
            await Clients.Group(roomName).SendAsync("PlayerLeft", Context.ConnectionId);
        }

        public async Task SendMove(string user, Position actualPosition, Position newPosition)
        {
            string room = playerRooms[Context.ConnectionId];
            var game = games[room];

            foreach (var gamePosition in game.positions)
            {
                if (gamePosition.piece != null && gamePosition.piece == actualPosition.piece)
                    gamePosition.piece.GetPossibleMove(game, newPosition);
            }
            // Validate and apply the move to the game logic
           
                // Broadcast the updated game state to all players in the room
                await Clients.Group(room).SendAsync("ReceiveMove", newPosition);
            
            else
            {
                // Handle invalid move
                await Clients.Caller.SendAsync("InvalidMove", "Invalid move, please try again.");
            }
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
}
