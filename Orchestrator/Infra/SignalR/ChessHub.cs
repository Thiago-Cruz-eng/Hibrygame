using Amazon.Runtime.Internal.Transform;
using Hibrygame;
using Hibrygame.Enums;
using Microsoft.AspNetCore.SignalR;

namespace Orchestrator.Infra.SignalR
{
    public class ChessHub : Hub
    {
        private static Dictionary<string, Board> games = new Dictionary<string, Board>();
        private static Dictionary<string, string> playerRooms = new Dictionary<string, string>();
        private static Dictionary<string, string> playersOnRoom = new();

        public string CreateRoom(string room)
        {
            return playerRooms[Context.ConnectionId] = room;
        }

        public List<string> GetAvailableRoom()
        {
            var availableRooms = new List<string>();
            foreach (var room in playerRooms.Values.Distinct())
            {
                if (playerRooms.Count(p => p.Value == room) < 2)
                {
                    availableRooms.Add(room);
                }
            }
            return availableRooms;
        }
        
        public async Task<JoinRoomResponse> JoinRoom(string playerName, string room)
        {
            if (!IsRoomFull(room))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, room);
                
                playersOnRoom.Add(playerName, room); 

                await Clients.All.SendAsync("PlayerJoined", playersOnRoom);
                
                return new JoinRoomResponse
                {
                    ConnectionId = Context.ConnectionId,
                    Player = playerName,
                    Room = room,
                    //Board = games[room]
                };
            }
            await Clients.Caller.SendAsync("RoomFull", "The room is full. Please try another room.");
            await StartGame(room, games[room]);
            return new JoinRoomResponse
            {
                ConnectionId = null,
                Player = null,
                Room = null,
                //Board = null
            };
        }
        
        public async Task EnterRoom(string connectionId, string playerName, bool ready)
        {
            var room = playerRooms[connectionId];
            if (games.ContainsKey(room) && IsRoomFull(room))
            {
                games[room].StartBoard(); 
                await Clients.Group(room).SendAsync("Ready");
            }
        }

        public async Task StartGame(string room, Board board)
        {
            if (games.ContainsKey(room) && IsRoomFull(room))
            {
                if (!games.ContainsKey(room))
                {
                    games.TryAdd(room, new Board());
                }
                games[room].StartBoard(); 
                await Clients.Group(room).SendAsync("StartGame", "Jogo iniciado");
            }
        }

        public async Task LeaveRoom(string roomName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomName);
            await Clients.Group(roomName).SendAsync("PlayerLeft", Context.ConnectionId);
        }

        public async Task SendMove(string user, Position actualPosition, Position newPosition)
        {
            var room = playerRooms[Context.ConnectionId];
            var game = games[room];

            foreach (var gamePosition in game.positions)
            {
                if (gamePosition.piece == null || gamePosition.piece != actualPosition.piece) continue;
                var possibleMoves = gamePosition.piece.GetPossibleMove(game, actualPosition);
                if (!possibleMoves.possibleMoves.Contains(newPosition)) continue;
                await Clients.Group(room).SendAsync("ReceiveMove", newPosition);
                return;
            }
            await Clients.Caller.SendAsync("InvalidMove", "Invalid move, please try again.");
        }
        
        private bool IsRoomFull(string room)
        {
            return playerRooms.Count(p => p.Value == room) == 2;
        }

        public class JoinRoomResponse
        {
            public string ConnectionId { get; set; }
            public string Player { get; set; }
            public string Room { get; set; }
            //public Board Board { get; set; }
        }
    }

   
}
