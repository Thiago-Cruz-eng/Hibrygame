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
        private static Dictionary<string, string> Rooms = new();
        private static Dictionary<string, string> playersInRoom = new();

        public string CreateRoom(string room)
        {
            return Rooms[Context.ConnectionId] = room;
        }

        public Dictionary<List<string>, string> GetAvailableRoom()
        {
                                               // players  room
            var availableRooms = new Dictionary<List<string>, string>();
            foreach (var room in Rooms)
            {
                var user = new List<string>();
                foreach (var player in playersInRoom)
                {
                    if (player.Value == room.Value)
                    {
                        user.Add(player.Value);
                        availableRooms.Add(user, room.Value);
                    }
                    else
                    {
                        availableRooms.Add(new List<string>(), room.Value);
                    }
                }
                if(playersInRoom.Count == 0) availableRooms.Add(new List<string>(), room.Value);
            }
            return availableRooms;
        }
        
        public async Task<JoinRoomResponse> JoinRoom(string playerName, string room)
        {
            if (!IsRoomFull(room))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, room);
                
                playersInRoom.Add(playerName, room); 

                await Clients.All.SendAsync("PlayerJoined", playersInRoom);
                
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
        
        public async Task ReadyToGame(string connectionId, bool ready)
        {
            var room = Rooms[connectionId];
            if (games.ContainsKey(room) && IsRoomFull(room))
            {
                await Clients.Group(room).SendAsync("Ready", connectionId);
                StartGame(room);
            }
        }

        public string StartGame(string room)
        {
            if (!games.ContainsKey(room) || !IsRoomFull(room)) return "Game cannot start";
            if (!games.ContainsKey(room))
            {
                games.TryAdd(room, new Board());
            }
            
            games[room].StartBoard();
            var game = games[room].GetPositionsPlaced();
            
            var gameJson = JsonConvert.SerializeObject(game);
            return gameJson;
        }

        public async Task LeaveRoom(string roomName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomName);
            await Clients.Group(roomName).SendAsync("PlayerLeft", Context.ConnectionId);
        }
        public async Task<List<Position>> SendPossiblesMove(string user, Position actualPosition)
        {
            var room = Rooms[Context.ConnectionId];
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

        public async Task<List<Position>> SendPossiblesMoves(string user, Position actualPosition)
        {
            var room = Rooms[Context.ConnectionId];
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
            var x = playersInRoom.Count();
            return playersInRoom.Count(p => p.Value == room) == 2;
        }

        public class JoinRoomResponse
        {
            public string ConnectionId { get; set; }
            public string Player { get; set; }
            public string Room { get; set; }
        }
    }

   
}
