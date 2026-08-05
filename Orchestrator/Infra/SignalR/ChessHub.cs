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
        // TryAdd responde exatamente o que o lobby precisa saber. A versao anterior
        // comparava `created != Rooms[room]`, sempre a mesma referencia, logo sempre
        // falso: a sala so era reportada como existente se ja tivesse jogadores.
        var alreadyExisted = !Rooms.TryAdd(room, new GameRoom(room));

        return Task.FromResult(new CreateRoomResponse
        {
            Room = Rooms[room].Name,
            AlreadyExisted = alreadyExisted
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

    /// <summary>
    /// Entra na sala. <paramref name="preferredColor"/> e a cor escolhida no lobby
    /// ("White"/"Black", ou null para deixar o servidor decidir) — atendida quando esta
    /// livre. Tambem serve para REENTRAR depois de uma reconexao: o SignalR volta com um
    /// ConnectionId novo, e chamar JoinRoom de novo devolve o assento.
    /// </summary>
    public async Task<JoinRoomResponse> JoinRoom(string playerName, string room, string? preferredColor)
    {
        if (!Rooms.TryGetValue(room, out var gameRoom))
        {
            await Clients.Caller.SendAsync("RoomNotFound", room);
            return JoinRoomResponse.Empty();
        }

        var preferred = ParseColor(preferredColor);
        var color = gameRoom.TryAssignColor(Context.ConnectionId, playerName, preferred);
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
            Color = color.ToString(),
            AssignedColor = color.ToString(),
            PreferenceHonoured = preferred is null || preferred == color
        };
    }

    private static ColorEnum? ParseColor(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "white" => ColorEnum.White,
        "black" => ColorEnum.Black,
        _ => null
    };

    public async Task<StartGameResponse> StartGame(string room)
    {
        if (!Rooms.TryGetValue(room, out var gameRoom))
            return StartGameResponse.Failure($"Room '{room}' not found.");

        if (!gameRoom.IsFull)
            return StartGameResponse.Failure("Room needs 2 players to start.");

        // Sob o lock: Start() reescreve as 64 casas, e o snapshot le todas elas. Fora do
        // lock, dois clientes chamando StartGame ao mesmo tempo — o que o frontend faz,
        // porque cada um chama ao conectar e a cada PlayerJoined — corrompiam o tabuleiro.
        var snapshot = await gameRoom.Serialized(() =>
        {
            gameRoom.Start();
            return BuildSnapshot(gameRoom);
        });

        await Clients.Group(room).SendAsync("GameStarted", snapshot);
        return new StartGameResponse { Success = true, Snapshot = snapshot };
    }

    public Task<BoardSnapshot?> GetBoardSnapshot(string room)
        => Rooms.TryGetValue(room, out var gameRoom)
            ? gameRoom.Serialized<BoardSnapshot?>(() => BuildSnapshot(gameRoom))
            : Task.FromResult<BoardSnapshot?>(null);

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

        // Mesma autoridade de servidor que MakeMove ja aplicava: so quem esta na sala
        // consulta o tabuleiro dela, e cada jogador so enumera as proprias pecas.
        if (!gameRoom.Players.TryGetValue(Context.ConnectionId, out var player))
            return Task.FromResult(PossibleMovesResponse.Failure("You are not in this room."));

        if (!Position.TryFromAlgebraic(from, out var fromPos) || fromPos is null)
            return Task.FromResult(PossibleMovesResponse.Failure($"Invalid square '{from}'."));

        return gameRoom.Serialized(() =>
        {
            var source = gameRoom.Board.GetPositionInBoard(fromPos.Row, fromPos.Column);
            if (source.Piece is null)
                return PossibleMovesResponse.Failure($"No piece on '{from}'.");

            if (source.Piece.Color != player.Color)
                return PossibleMovesResponse.Failure("That piece is not yours.");

            var (moves, _) = source.Piece.GetPossibleMove(gameRoom.Board, source);
            return new PossibleMovesResponse
            {
                Success = true,
                From = source.Algebraic,
                Moves = moves?.Select(MapSquare).ToList() ?? new List<SquareDto>()
            };
        });
    }

    /// <summary>
    /// Aplica um lance, se ele for legal, e avisa a sala.
    ///
    /// <para>
    /// <b>Autoridade do servidor (principio II da constituicao).</b> O cliente diz apenas "de
    /// onde" e "para onde"; tudo o mais e reconferido aqui. A lista de lances que o frontend
    /// tenha calculado e ignorada — o servidor recalcula com
    /// <c>GetPossibleMove</c>. Metodo de hub novo que altere o tabuleiro repete as seis
    /// checagens: partida em andamento, identidade, notacao, turno, posse e legalidade.
    /// </para>
    ///
    /// <para>
    /// A leitura fica em tres partes, nesta ordem: as guardas baratas que nao tocam no
    /// tabuleiro (aqui), a aplicacao sob o lock (<see cref="ApplyMove"/>) e a difusao
    /// (<see cref="BroadcastMoveAsync"/>). A separacao existe porque so a parte do meio precisa
    /// de exclusividade — ver a nota de <c>GameRoom.Serialized</c>.
    /// </para>
    /// </summary>
    /// <param name="from">Casa de origem em notacao algebrica, como <c>"e2"</c>.</param>
    /// <param name="to">Casa de destino, como <c>"e4"</c>.</param>
    public async Task<MakeMoveResponse> MakeMove(string room, string from, string to)
    {
        // Guardas que nao dependem do tabuleiro, portanto fora do lock.
        if (!Rooms.TryGetValue(room, out var gameRoom) || !gameRoom.Started)
            return MakeMoveResponse.Failure("Game not started.");

        if (gameRoom.Finished)
            return MakeMoveResponse.Failure("Game already finished.");

        // Identidade: a conexao tem de ter assento nesta sala. Nao basta estar autenticado.
        if (!gameRoom.Players.TryGetValue(Context.ConnectionId, out var player))
            return MakeMoveResponse.Failure("You are not in this room.");

        if (!TryParseSquares(from, to, out var fromPos, out var toPos))
            return MakeMoveResponse.Failure("Invalid square notation.");

        // Verificar o turno, validar, aplicar e trocar o turno tem de ser um bloco
        // indivisivel. Estando separados por um await, dois lances submetidos ao mesmo
        // tempo passavam ambos pela verificacao de turno e o mesmo jogador jogava duas
        // vezes na mesma vez.
        var outcome = await gameRoom.Serialized(() => ApplyMove(gameRoom, player, fromPos, toPos, from));

        // Lance recusado nao vira evento: quem errou recebe o motivo no retorno da propria
        // chamada, e o resto da sala nao precisa saber.
        if (!outcome.Success) return outcome;

        await BroadcastMoveAsync(room, player, outcome);

        return outcome;
    }

    /// <summary>
    /// Converte as duas casas de notacao algebrica para indices do tabuleiro.
    ///
    /// <para>
    /// Usa a variante <c>Try</c> porque a entrada vem do cliente: notacao invalida e resposta de
    /// erro, nao excecao. Exige as duas de uma vez — nao ha lance com metade das coordenadas.
    /// </para>
    /// </summary>
    private static bool TryParseSquares(
        string from,
        string to,
        out Position fromPos,
        out Position toPos)
    {
        fromPos = null!;
        toPos = null!;

        if (!Position.TryFromAlgebraic(from, out var parsedFrom) || parsedFrom is null)
            return false;

        if (!Position.TryFromAlgebraic(to, out var parsedTo) || parsedTo is null)
            return false;

        fromPos = parsedFrom;
        toPos = parsedTo;
        return true;
    }

    /// <summary>
    /// Valida e aplica o lance no tabuleiro da sala.
    ///
    /// <para>
    /// <b>Corre sempre dentro de <c>GameRoom.Serialized</c></b> — nunca chame direto. Tudo aqui
    /// toca o tabuleiro compartilhado, e a avaliacao de legalidade simula lances nele.
    /// </para>
    ///
    /// <para>
    /// A ordem das checagens e deliberada: turno antes de posse, posse antes de legalidade. Cada
    /// uma e mais caro que a anterior, e recusar cedo evita calcular lances possiveis para um
    /// pedido que ja estava recusado.
    /// </para>
    /// </summary>
    /// <param name="fromLabel">
    /// A notacao original da origem, so para compor a mensagem de erro com o texto que o cliente
    /// enviou.
    /// </param>
    private static MakeMoveResponse ApplyMove(
        GameRoom gameRoom,
        GameRoom.PlayerSlot player,
        Position fromPos,
        Position toPos,
        string fromLabel)
    {
        if (player.Color != gameRoom.CurrentTurn)
            return MakeMoveResponse.Failure("Not your turn.");

        var source = gameRoom.Board.GetPositionInBoard(fromPos.Row, fromPos.Column);
        if (source.Piece is null)
            return MakeMoveResponse.Failure($"No piece on '{fromLabel}'.");

        // Posse: cada jogador move so as proprias pecas.
        if (source.Piece.Color != player.Color)
            return MakeMoveResponse.Failure("That piece is not yours.");

        var target = gameRoom.Board.GetPositionInBoard(toPos.Row, toPos.Column);

        // Legalidade recalculada no servidor. O que o cliente ache que e legal nao entra na
        // decisao.
        var (possibleMoves, _) = source.Piece.GetPossibleMove(gameRoom.Board, source);
        if (possibleMoves is null || !possibleMoves.Any(p => p.Row == target.Row && p.Column == target.Column))
            return MakeMoveResponse.Failure("Illegal move.");

        // Move.MakeMove aplica e, se o lance expuser o proprio rei, desfaz — deixando o
        // tabuleiro exatamente como estava. Por isso um false aqui e seguro.
        if (!Move.MakeMove(gameRoom.Board, possibleMoves, target, source))
            return MakeMoveResponse.Failure("Move would leave king in check.");

        gameRoom.SwitchTurn();

        // Fim de partida e sempre avaliado do ponto de vista de quem TEM a vez agora:
        // sem lance legal e em xeque e mate, sem xeque e afogamento.
        //
        // Apurado aqui, uma vez por lance e dentro do lock, e guardado na sala. Antes
        // BuildSnapshot recalculava a cada leitura — e como EvaluateOutcome simula
        // lances no tabuleiro, isso fazia de "montar snapshot" uma escrita.
        var evaluated = Move.EvaluateOutcome(gameRoom.Board, gameRoom.CurrentTurn);
        gameRoom.SetOutcome(evaluated);

        return new MakeMoveResponse
        {
            Success = true,
            From = source.Algebraic,
            To = target.Algebraic,
            NextTurn = gameRoom.CurrentTurn.ToString(),
            Outcome = evaluated.ToString(),

            // Quem venceu e quem acabou de jogar: SwitchTurn ja passou a vez, e o mate e
            // avaliado sobre quem RECEBEU a vez e nao tem lance.
            Winner = evaluated == GameOutcome.Checkmate ? player.Color.ToString() : null,

            Snapshot = BuildSnapshot(gameRoom)
        };
    }

    /// <summary>
    /// Avisa a sala do lance aplicado — e, se a partida acabou, avisa disso tambem.
    ///
    /// <para>
    /// Fora do lock de propriedade: difundir e E/S de rede e nao toca o tabuleiro. Manter isto
    /// dentro do lock seguraria os outros jogadores da sala pelo tempo de uma ida a rede.
    /// </para>
    /// </summary>
    private async Task BroadcastMoveAsync(string room, GameRoom.PlayerSlot player, MakeMoveResponse outcome)
    {
        await Clients.Group(room).SendAsync("BoardChanged", new
        {
            From = outcome.From,
            To = outcome.To,
            ByColor = player.Color.ToString(),
            NextTurn = outcome.NextTurn,
            Outcome = outcome.Outcome,
            Winner = outcome.Winner,
            Snapshot = outcome.Snapshot
        });

        // Evento proprio para o fim: o frontend nao precisa inspecionar todo BoardChanged
        // para descobrir que a partida acabou.
        if (outcome.Outcome != GameOutcome.InProgress.ToString())
        {
            await Clients.Group(room).SendAsync("GameOver", new
            {
                Room = room,
                Outcome = outcome.Outcome,
                Winner = outcome.Winner,
                Snapshot = outcome.Snapshot
            });
        }
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
            // Lido do estado da sala, nao recalculado: EvaluateOutcome simula lances no
            // tabuleiro, e montar o snapshot tem de ser leitura pura. Ver GameRoom.Outcome.
            Outcome = gameRoom.Outcome.ToString(),
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

        /// <summary>Cor efetivamente atribuida. Campo historico, mantido para nao quebrar o FE.</summary>
        public string? Color { get; set; }

        /// <summary>Igual a <see cref="Color"/>, com nome que diz que a decisao e do servidor.</summary>
        public string? AssignedColor { get; set; }

        /// <summary>Falso quando a cor pedida no lobby estava tomada e outra foi atribuida.</summary>
        public bool PreferenceHonoured { get; set; }

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

        /// <summary>"InProgress", "Checkmate" ou "Stalemate", na vez de <see cref="NextTurn"/>.</summary>
        public string? Outcome { get; set; }

        /// <summary>Cor vencedora quando <see cref="Outcome"/> e "Checkmate"; null nos outros casos.</summary>
        public string? Winner { get; set; }

        public BoardSnapshot? Snapshot { get; set; }

        public static MakeMoveResponse Failure(string message) => new() { Success = false, Message = message };
    }

    public class BoardSnapshot
    {
        public string Room { get; set; } = string.Empty;
        public string CurrentTurn { get; set; } = string.Empty;
        public bool Started { get; set; }
        public bool Finished { get; set; }

        /// <summary>"InProgress", "Checkmate" ou "Stalemate", na vez de <see cref="CurrentTurn"/>.</summary>
        public string Outcome { get; set; } = nameof(GameOutcome.InProgress);

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
