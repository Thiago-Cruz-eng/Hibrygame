using System.Collections.Concurrent;
using Hibrygame;
using Hibrygame.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Orchestrator.Infra.SignalR;

[Authorize(Policy = "Role:Player")]
public class ChessHub : Hub
{
    private static readonly ConcurrentDictionary<string, GameRoom> Rooms = new();

    public Task<CreateRoomResponse> CreateRoom(string room)
    {
        var created = Rooms.GetOrAdd(room, name => new GameRoom(name));
        return Task.FromResult(new CreateRoomResponse
        {
            Room = created.Name,
            AlreadyExisted = created != Rooms[room] || created.Players.Count > 0
        });
    }

    public Task<List<string>> GetAvailableRooms()
        => Task.FromResult(Rooms.Values.Where(r => !r.IsFull && !r.Finished).Select(r => r.Name).ToList());

    public Task<Dictionary<string, List<string>>> GetPlayersInEachRoom()
    {
        var snapshot = Rooms.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.Players.Values.Select(p => p.Name).ToList());
        return Task.FromResult(snapshot);
    }

    public Task<int> GetPlayersInRoom(string room)
        => Task.FromResult(Rooms.TryGetValue(room, out var r) ? r.Players.Count : 0);

    public async Task<JoinRoomResponse> JoinRoom(string playerName, string room)
    {
        if (!Rooms.TryGetValue(room, out var gameRoom))
        {
            await Clients.Caller.SendAsync("RoomNotFound", room);
            return JoinRoomResponse.Empty();
        }

        var color = gameRoom.TryAssignColor(Context.ConnectionId, playerName);
        if (color is null)
        {
            await Clients.Caller.SendAsync("RoomFull", "The room is full. Please try another room.");
            return JoinRoomResponse.Empty();
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, room);
        await Clients.Group(room).SendAsync("PlayerJoined", new
        {
            Room = room,
            Player = playerName,
            Color = color.ToString(),
            Players = gameRoom.Players.Values.Select(p => new { p.Name, Color = p.Color.ToString() })
        });

        return new JoinRoomResponse
        {
            ConnectionId = Context.ConnectionId,
            Player = playerName,
            Room = room,
            Color = color.ToString()
        };
    }

    public async Task<StartGameResponse> StartGame(string room)
    {
        if (!Rooms.TryGetValue(room, out var gameRoom))
            return StartGameResponse.Failure($"Room '{room}' not found.");

        if (!gameRoom.IsFull)
            return StartGameResponse.Failure("Room needs 2 players to start.");

        gameRoom.Start();
        var snapshot = BuildSnapshot(gameRoom);
        await Clients.Group(room).SendAsync("GameStarted", snapshot);
        return new StartGameResponse { Success = true, Snapshot = snapshot };
    }

    public Task<BoardSnapshot?> GetBoardSnapshot(string room)
        => Task.FromResult(Rooms.TryGetValue(room, out var gameRoom) ? BuildSnapshot(gameRoom) : null);

    public async Task LeaveRoom(string room)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, room);
        if (Rooms.TryGetValue(room, out var gameRoom))
        {
            gameRoom.Remove(Context.ConnectionId);
            await Clients.Group(room).SendAsync("PlayerLeft", new
            {
                Room = room,
                ConnectionId = Context.ConnectionId,
                Players = gameRoom.Players.Values.Select(p => new { p.Name, Color = p.Color.ToString() })
            });
        }
    }

    public Task<PossibleMovesResponse> GetPossibleMoves(string room, string from)
    {
        if (!Rooms.TryGetValue(room, out var gameRoom) || !gameRoom.Started)
            return Task.FromResult(PossibleMovesResponse.Failure("Game not started."));

        if (!Position.TryFromAlgebraic(from, out var fromPos) || fromPos is null)
            return Task.FromResult(PossibleMovesResponse.Failure($"Invalid square '{from}'."));

        var source = gameRoom.Board.GetPositionInBoard(fromPos.Row, fromPos.Column);
        if (source.Piece is null)
            return Task.FromResult(PossibleMovesResponse.Failure($"No piece on '{from}'."));

        var (moves, _) = source.Piece.GetPossibleMove(gameRoom.Board, source);
        return Task.FromResult(new PossibleMovesResponse
        {
            Success = true,
            From = source.Algebraic,
            Moves = moves?.Select(MapSquare).ToList() ?? new List<SquareDto>()
        });
    }

    public async Task<MakeMoveResponse> MakeMove(string room, string from, string to)
    {
        if (!Rooms.TryGetValue(room, out var gameRoom) || !gameRoom.Started)
            return MakeMoveResponse.Failure("Game not started.");

        if (gameRoom.Finished)
            return MakeMoveResponse.Failure("Game already finished.");

        if (!gameRoom.Players.TryGetValue(Context.ConnectionId, out var player))
            return MakeMoveResponse.Failure("You are not in this room.");

        if (player.Color != gameRoom.CurrentTurn)
            return MakeMoveResponse.Failure("Not your turn.");

        if (!Position.TryFromAlgebraic(from, out var fromPos) || fromPos is null ||
            !Position.TryFromAlgebraic(to, out var toPos) || toPos is null)
            return MakeMoveResponse.Failure("Invalid square notation.");

        var source = gameRoom.Board.GetPositionInBoard(fromPos.Row, fromPos.Column);
        if (source.Piece is null)
            return MakeMoveResponse.Failure($"No piece on '{from}'.");

        if (source.Piece.Color != player.Color)
            return MakeMoveResponse.Failure("That piece is not yours.");

        var target = gameRoom.Board.GetPositionInBoard(toPos.Row, toPos.Column);
        var (possibleMoves, _) = source.Piece.GetPossibleMove(gameRoom.Board, source);
        if (possibleMoves is null || !possibleMoves.Any(p => p.Row == target.Row && p.Column == target.Column))
            return MakeMoveResponse.Failure("Illegal move.");

        var applied = await Move.MakeMove(gameRoom.Board, possibleMoves, target, source);
        if (!applied)
            return MakeMoveResponse.Failure("Move would leave king in check.");

        gameRoom.SwitchTurn();

        var snapshot = BuildSnapshot(gameRoom);
        await Clients.Group(room).SendAsync("BoardChanged", new
        {
            From = source.Algebraic,
            To = target.Algebraic,
            ByColor = player.Color.ToString(),
            NextTurn = gameRoom.CurrentTurn.ToString(),
            Snapshot = snapshot
        });

        return new MakeMoveResponse
        {
            Success = true,
            From = source.Algebraic,
            To = target.Algebraic,
            NextTurn = gameRoom.CurrentTurn.ToString(),
            Snapshot = snapshot
        };
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var (name, gameRoom) in Rooms)
        {
            if (gameRoom.Players.TryRemove(Context.ConnectionId, out var slot))
            {
                await Clients.Group(name).SendAsync("PlayerLeft", new
                {
                    Room = name,
                    ConnectionId = Context.ConnectionId,
                    Player = slot.Name,
                    Players = gameRoom.Players.Values.Select(p => new { p.Name, Color = p.Color.ToString() })
                });
            }
        }
        await base.OnDisconnectedAsync(exception);
    }

    private static BoardSnapshot BuildSnapshot(GameRoom gameRoom)
    {
        var squares = new List<SquareDto>();
        foreach (var position in gameRoom.Board.Positions)
        {
            if (position is null) continue;
            squares.Add(MapSquare(position));
        }
        return new BoardSnapshot
        {
            Room = gameRoom.Name,
            CurrentTurn = gameRoom.CurrentTurn.ToString(),
            Started = gameRoom.Started,
            Finished = gameRoom.Finished,
            Squares = squares
        };
    }

    private static SquareDto MapSquare(Position position) => new()
    {
        File = position.File.ToString(),
        Rank = position.Rank,
        Algebraic = position.Algebraic,
        Row = position.Row,
        Column = position.Column,
        SquareColor = position.SquareColor.ToString(),
        Piece = position.Piece is null
            ? null
            : new PieceDto
            {
                Type = position.Piece.Type.ToString(),
                Color = position.Piece.Color.ToString(),
                IsInCheckState = position.Piece.IsInCheckState
            }
    };

    public class CreateRoomResponse
    {
        public string Room { get; set; } = string.Empty;
        public bool AlreadyExisted { get; set; }
    }

    public class JoinRoomResponse
    {
        public string? ConnectionId { get; set; }
        public string? Player { get; set; }
        public string? Room { get; set; }
        public string? Color { get; set; }

        public static JoinRoomResponse Empty() => new();
    }

    public class StartGameResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public BoardSnapshot? Snapshot { get; set; }

        public static StartGameResponse Failure(string message) => new() { Success = false, Message = message };
    }

    public class PossibleMovesResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? From { get; set; }
        public List<SquareDto> Moves { get; set; } = new();

        public static PossibleMovesResponse Failure(string message) => new() { Success = false, Message = message };
    }

    public class MakeMoveResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? From { get; set; }
        public string? To { get; set; }
        public string? NextTurn { get; set; }
        public BoardSnapshot? Snapshot { get; set; }

        public static MakeMoveResponse Failure(string message) => new() { Success = false, Message = message };
    }

    public class BoardSnapshot
    {
        public string Room { get; set; } = string.Empty;
        public string CurrentTurn { get; set; } = string.Empty;
        public bool Started { get; set; }
        public bool Finished { get; set; }
        public List<SquareDto> Squares { get; set; } = new();
    }

    public class SquareDto
    {
        public string File { get; set; } = string.Empty;
        public int Rank { get; set; }
        public string Algebraic { get; set; } = string.Empty;
        public int Row { get; set; }
        public int Column { get; set; }
        public string SquareColor { get; set; } = string.Empty;
        public PieceDto? Piece { get; set; }
    }

    public class PieceDto
    {
        public string Type { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public bool IsInCheckState { get; set; }
    }
}
