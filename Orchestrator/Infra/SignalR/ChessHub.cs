using Hibrygame;
using Hibrygame.Enums;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;

namespace Orchestrator.Infra.SignalR
{
    public class ChessHub : Hub
    {                           //room name / board game
        private static Dictionary<string, Board> games = new();
                                //conectionId / room name
        private static Dictionary<string, string> Rooms = new();
                                //playerName / roomName
        private static Dictionary<string, string> playersInRoom = new();

        public async Task<string> CreateRoom(string room)
        {
            var newRoom = Rooms[Context.ConnectionId] = room;
            await Clients.All.SendAsync("CreateRoom", newRoom);
            return newRoom;
        }

        public List<string> GetAvailableRoom()
        {
            var availableRooms = new List<string>();
            foreach (var room in Rooms.Values.Distinct())
            {
                if (Rooms.Count > 0)
                {
                    availableRooms.Add(room);
                }
            }
            return availableRooms;
        }

        public Dictionary<string, string> GetPlayersInEachRoom()
        {
            return playersInRoom;
        }
        
        public int GetPlayersInRoom(string room)
        {
            return playersInRoom.Values.Count(p => p == room);
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

        public async Task<string> StartGame(string room)
        {
            //if (!games.ContainsKey(room) || !IsRoomFull(room)) return "Game cannot start";
            if (!games.ContainsKey(room))
            {
                games.TryAdd(room, new Board());
                games[room].StartBoard();
                games[room].MakePieceInInitialState();
            }
            
            var game = games[room].GetPositionsPlaced();
            await Clients.Group(room).SendAsync("GameWillStart", room);
            
            var gameJson = JsonConvert.SerializeObject(game);
            return gameJson;
        }

        public async Task LeaveRoom(string roomName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomName);
            await Clients.Group(roomName).SendAsync("PlayerLeft", Context.ConnectionId);
        }

        public class PosDto
        {
            public int Row { get; set; } 
            public int Column { get; set; } 
        }

        public class PieceDto
        {
            public PieceEnum Type { get; set; } 
            public ColorEnum Color { get; set; } 
            public bool IsInCheckState { get; set; }
        }

        public async Task<List<Position>> SendPossiblesMoves(string user, string room, int row, int column)
        {
            var connect = "";
            foreach (var rooms in Rooms.Where(rooms => rooms.Value == room))
            {
                connect = rooms.Key;
            }
            var roomName = Rooms[connect];
            var game = games[roomName];
            var actualPositionInBoard = game.GetPositionInBoard(row, column);
            var actualPosition = new Position(actualPositionInBoard.Row, actualPositionInBoard.Column);
            actualPosition.SquareColor = actualPositionInBoard.SquareColor;
            actualPosition.Piece = actualPositionInBoard.Piece;

            foreach (var gamePosition in game.Positions)
            {
                if (gamePosition.Piece == null || gamePosition.Piece != actualPosition.Piece) continue;
                var possibleMoves = gamePosition.Piece.GetPossibleMove(game, actualPosition);
                return possibleMoves.possibleMoves;
            }
            await Clients.Caller.SendAsync("InvalidMove", "Invalid move, please try again.");
            return new List<Position>();
        }
        
        public async Task<bool> MakeMove(string user, string room, int startRow, int endRow, int startColumn, int endColumn, bool isHighlight)
        {
            if (isHighlight) return false;
            var newPosition = GetPositionInBoard(room, endRow, endColumn);
            var oldPosition = GetPositionInBoard(room, startRow, startColumn);
            var possibleMoves = await SendPossiblesMoves(user, room, startRow, startColumn);
            if (!possibleMoves.Contains(newPosition)) return false;
            var game = games[room];
            var makeMove = game.MakeMove(game, possibleMoves, newPosition, oldPosition);
            await Clients.Group(room).SendAsync("BoardChange", true);
            return true;
        }
        
        public Position GetPositionInBoard(string room, int row, int column)
        {
            var game = games[room];
            return game.GetPositionInBoard(row, column);
        }
        public string GetPositionPlaced(string room)
        {
            var game = games[room];
            var gameJson = JsonConvert.SerializeObject(game);
            return gameJson;
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
            //AO RENDERIZAR A TELA UM NOVO GAME/INSTACIA É CHAMADA
            //AO PEDIR O SMOVIMENTOS DO CAVALO QUANDO PEAO NA FRENTE, ELE BUGA 
            //AO MOVER O BISPO, CAVALO BUGA E RAINHA BUGA 
        }
    }
}
