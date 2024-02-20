using Amazon.Runtime.Internal.Transform;
using Hibrygame;
using Hibrygame.Enums;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Orchestrator.Infra.SignalR
{
    public class ChessHub : Hub
    {
        private static Dictionary<string, Board> games = new();
        private static Dictionary<string, string> playerRooms = new();
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
                    Room = room
                };
            }
            await Clients.Caller.SendAsync("RoomFull", "The room is full. Please try another room.");
            return new JoinRoomResponse
            {
                ConnectionId = null,
                Player = null,
                Room = null,
            };
        }
        
        public async Task ReadyToGame(string connectionId, string playerName, bool ready)
        {
            var room = playerRooms[connectionId];
            if (games.ContainsKey(room) && IsRoomFull(room))
            {
                await Clients.Group(room).SendAsync("Ready", connectionId);
            }
        }

        public string StartGame(string room, Board board)
        {
            if (!games.ContainsKey(room) || !IsRoomFull(room)) return "Game cannot start";
            if (!games.ContainsKey(room))
            {
                games.TryAdd(room, new Board());
            }
            var game = games[room].StartBoard();
            var gameJson = JsonConvert.SerializeObject(game);
            return gameJson;
        }

        public async Task LeaveRoom(string roomName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomName);
            await Clients.Group(roomName).SendAsync("PlayerLeft", Context.ConnectionId);
        }

        public async Task<List<Position>> SendPossiblesMoves(string user, Position actualPosition)
        {
            var room = playerRooms[Context.ConnectionId];
            var game = games[room];

            foreach (var gamePosition in game.Positions)
            {
                if (gamePosition.Piece == null || gamePosition.Piece != actualPosition.Piece) continue;
                var possibleMoves = gamePosition.Piece.GetPossibleMove(game, actualPosition);
                return possibleMoves.possibleMoves;
            }
            await Clients.Caller.SendAsync("InvalidMove", "Invalid move, please try again.");
            return new List<Position>();
        }
        
        public async Task<bool> MakeMove(string user, Position newPosition, Position actualPosition)
        {
            var possibleMoves = await SendPossiblesMoves(user, actualPosition);
            return possibleMoves.Contains(newPosition);
        }
        
        private bool IsRoomFull(string room)
        {
            var x = playersOnRoom.Count();
            return playersOnRoom.Count(p => p.Value == room) == 1;
        }

        public class JoinRoomResponse
        {
            public string ConnectionId { get; set; }
            public string Player { get; set; }
            public string Room { get; set; }
        }
    }

   
}
