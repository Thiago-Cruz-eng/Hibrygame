using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Text.RegularExpressions;
using Hibrygame;
using Hibrygame.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Orchestrator.Infra.SignalR;

[Authorize(Policy = "Role:Player")]
public partial class ChessHub : Hub
{
    private static readonly ConcurrentDictionary<string, GameRoom> Rooms = new();

    /// <summary>
    /// Teto de salas no processo inteiro.
    ///
    /// <para>
    /// O dicionario e <c>static</c> e nunca encolhe sozinho: sem teto, criar sala e uma forma
    /// gratuita de consumir memoria do servidor ate derruba-lo, e cada <c>GameRoom</c> carrega um
    /// tabuleiro de 64 casas. O numero e folgado — nunca houve nem perto disso em uso real — e
    /// existe para que exista um limite, nao para apertar ninguem.
    /// </para>
    /// </summary>
    private const int MaxRooms = 500;

    /// <summary>
    /// Teto de salas <b>ainda nao iniciadas</b> por usuario.
    ///
    /// <para>
    /// Sala iniciada nao conta: ela tem dois jogadores e some do dicionario quando os dois saem.
    /// O que se limita aqui e a sala que fica pendurada esperando adversario — e que, sem teto,
    /// um cliente cria em laco para entupir o lobby de todo mundo.
    /// </para>
    /// </summary>
    private const int MaxPendingRoomsPerUser = 5;

    /// <summary>
    /// Quanto tempo uma sala vazia e nunca iniciada e considerada "ainda em uso".
    ///
    /// <para>
    /// Existe por causa de uma janela real do fluxo do lobby: criar sala e entrar nela sao duas
    /// chamadas separadas, com o usuario escolhendo a cor no meio. Entre uma e outra a sala esta
    /// vazia, e recuperar salas vazias sem esta carencia apagaria a sala que a pessoa acabou de
    /// criar, no intervalo entre os dois cliques.
    /// </para>
    ///
    /// <para>
    /// Passada a carencia, uma sala vazia e nunca iniciada e lixo: ninguem entrou e ninguem vai
    /// entrar. Ela deixa de contar para o teto do criador e sai do dicionario na proxima criacao
    /// dele.
    /// </para>
    /// </summary>
    private static readonly TimeSpan AbandonedRoomGrace = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Nomes de sala aceitos: letras e digitos de qualquer alfabeto, espaco, hifen e sublinhado,
    /// de 1 a 64 caracteres.
    ///
    /// <para>
    /// O nome da sala circula como chave de grupo do SignalR, aparece na tela de todos os
    /// jogadores do lobby e entra em log. Texto livre nesses tres lugares e convite a HTML
    /// injetado na interface alheia, a nome de 10 MB e a forja de linha de log. A lista permissiva
    /// de letras Unicode (<c>\p{L}</c>) mantem acentuacao e outros alfabetos funcionando.
    /// </para>
    /// </summary>
    [GeneratedRegex(@"^[\p{L}\p{N} _-]{1,64}$")]
    private static partial Regex RoomNamePattern();

    /// <summary>
    /// Cria a sala, se o nome servir e o chamador nao estiver acima dos tetos.
    /// </summary>
    /// <param name="room">
    /// Nome pedido. Espacos nas pontas sao removidos antes de qualquer coisa — e o nome ja
    /// aparado que vira chave, para que <c>"sala"</c> e <c>"sala "</c> nao virem duas salas
    /// visualmente identicas.
    /// </param>
    /// <returns>
    /// <c>Success = false</c> com <c>Message</c> quando o nome nao serve ou um teto foi atingido.
    /// <c>AlreadyExisted</c> continua significando "ja havia uma sala com este nome".
    /// </returns>
    public Task<CreateRoomResponse> CreateRoom(string room)
    {
        var name = (room ?? string.Empty).Trim();

        if (!RoomNamePattern().IsMatch(name))
        {
            return Task.FromResult(CreateRoomResponse.Rejected(
                "Room name must be 1-64 characters, using letters, digits, space, '-' or '_'."));
        }

        var creator = CallerUserId();

        // Salas abandonadas do proprio chamador saem antes da contagem: elas nao representam
        // ninguem esperando partida. So as dele — varrer as dos outros mexeria em estado que
        // esta chamada nao tem por que tocar.
        if (creator is not null) ReclaimAbandonedRooms(creator);

        if (Rooms.Count >= MaxRooms)
            return Task.FromResult(CreateRoomResponse.Rejected("The server is at its room limit."));

        if (creator is not null && CountPendingRooms(creator) >= MaxPendingRoomsPerUser)
        {
            return Task.FromResult(CreateRoomResponse.Rejected(
                "You already have too many rooms waiting for players."));
        }

        // TryAdd responde exatamente o que o lobby precisa saber. A versao anterior
        // comparava `created != Rooms[room]`, sempre a mesma referencia, logo sempre
        // falso: a sala so era reportada como existente se ja tivesse jogadores.
        var alreadyExisted = !Rooms.TryAdd(name, new GameRoom(name, creator));

        return Task.FromResult(new CreateRoomResponse
        {
            Success = true,
            Room = name,
            AlreadyExisted = alreadyExisted
        });
    }

    /// <summary>
    /// Quantas salas do usuario estao esperando adversario.
    /// </summary>
    private static int CountPendingRooms(string userId) =>
        Rooms.Values.Count(gameRoom =>
            gameRoom.CreatedBy == userId && !gameRoom.Started && !gameRoom.Finished);

    /// <summary>
    /// Remove as salas do usuario que estao vazias, nunca iniciadas e alem da carencia.
    /// </summary>
    private static void ReclaimAbandonedRooms(string userId)
    {
        var cutoff = DateTime.UtcNow - AbandonedRoomGrace;

        foreach (var (name, gameRoom) in Rooms)
        {
            if (gameRoom.CreatedBy == userId
                && !gameRoom.Started
                && !gameRoom.Finished
                && gameRoom.Players.IsEmpty
                && gameRoom.CreatedAt < cutoff)
            {
                Rooms.TryRemove(name, out _);
            }
        }
    }

    /// <summary>
    /// O id do usuario autenticado nesta conexao, ou <c>null</c>.
    ///
    /// <para>
    /// <b>Nao use <c>Context.UserIdentifier</c>:</b> ele devolve o claim que o ASP.NET considera
    /// o nome do usuario, e como <c>MapInboundClaims</c> esta desligado (ver
    /// <c>JwtComposition</c>) o <c>sub</c> chega com o nome <c>sub</c> e nao e mapeado — o que faz
    /// <c>UserIdentifier</c> ser sempre <c>null</c>. O claim tem de ser procurado pelo nome.
    /// </para>
    /// </summary>
    private string? CallerUserId()
    {
        var sub = Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return string.IsNullOrWhiteSpace(sub) ? null : sub;
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
        // Nome maior que o teto nao corresponde a sala nenhuma — CreateRoom nao aceita criar uma
        // assim. Recusar antes da busca evita levar texto arbitrariamente grande adiante.
        if (room is null || room.Length > MaxRoomNameLength || !Rooms.TryGetValue(room, out var gameRoom))
        {
            await Clients.Caller.SendAsync("RoomNotFound", room);
            return JoinRoomResponse.Empty();
        }

        // O nome exibido vem do token, nao do cliente: era string livre, e um jogador podia
        // entrar na sala se apresentando com o nome do adversario.
        var displayName = ResolvePlayerName(playerName);

        var preferred = ParseColor(preferredColor);
        var color = gameRoom.TryAssignColor(Context.ConnectionId, displayName, preferred);
        if (color is null)
        {
            await Clients.Caller.SendAsync("RoomFull", "The room is full. Please try another room.");
            return JoinRoomResponse.Empty();
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, room);
        await Clients.Group(room).SendAsync("PlayerJoined", new
        {
            Room = room,
            Player = displayName,
            Color = color.ToString(),
            Players = gameRoom.Players.Values.Select(p => new { p.Name, Color = p.Color.ToString() })
        });

        return new JoinRoomResponse
        {
            ConnectionId = Context.ConnectionId,
            Player = displayName,
            Room = room,
            Color = color.ToString(),
            AssignedColor = color.ToString(),
            PreferenceHonoured = preferred is null || preferred == color
        };
    }

    /// <summary>Teto do nome de sala. Espelha o quantificador de <see cref="RoomNamePattern"/>.</summary>
    private const int MaxRoomNameLength = 64;

    /// <summary>
    /// O nome com que o jogador aparece para a sala.
    ///
    /// <para>
    /// <b>Preferencia absoluta pelo claim <c>name</c> do token.</b> O parametro
    /// <paramref name="requested"/> continua na assinatura porque e contrato com o frontend, mas
    /// ele so e usado quando o token nao traz nome — o que nao acontece com token emitido por
    /// este servidor (<c>TokenService</c> sempre inclui <c>name</c>). Sem isso, o nome exibido era
    /// texto livre do cliente: dava para entrar numa sala se apresentando como o adversario.
    /// </para>
    /// </summary>
    private string ResolvePlayerName(string? requested)
    {
        var fromToken = Context.User?.FindFirst("name")?.Value;
        var chosen = string.IsNullOrWhiteSpace(fromToken) ? requested : fromToken;

        return Truncate((chosen ?? string.Empty).Trim(), MaxRoomNameLength);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

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

            DiscardIfSpent(room, gameRoom);
        }
    }

    /// <summary>
    /// Tira do dicionario a sala que acabou de esvaziar e ja nao serve para mais nada.
    ///
    /// <para>
    /// <b>Exige as duas condicoes:</b> estar vazia e ja ter comecado (ou terminado). Sala
    /// iniciada e vazia e uma partida cujos dois jogadores foram embora — nao ha o que retomar,
    /// porque nao ha reentrada por identidade, so por <c>ConnectionId</c>. Ja a sala <b>nunca
    /// iniciada</b> e vazia e o estado normal entre criar e entrar, e apagar ali quebraria o
    /// fluxo do lobby; dessas cuida a recuperacao por carencia em <see cref="CreateRoom"/>.
    /// </para>
    ///
    /// <para>
    /// Antes nenhuma sala saia: o dicionario so crescia enquanto o processo vivesse.
    /// </para>
    /// </summary>
    private static void DiscardIfSpent(string room, GameRoom gameRoom)
    {
        if (gameRoom.Players.IsEmpty && (gameRoom.Started || gameRoom.Finished))
        {
            Rooms.TryRemove(room, out _);
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

                DiscardIfSpent(name, gameRoom);
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
        /// <summary>
        /// A sala foi criada, ou ja existia e pode ser usada.
        ///
        /// <para>
        /// Campo novo. Antes esta resposta nao tinha como recusar nada — toda chamada criava a
        /// sala. Cliente antigo que ignore este campo ve <c>Room</c> vazio na recusa, que era o
        /// unico sinal disponivel.
        /// </para>
        /// </summary>
        public bool Success { get; set; }

        /// <summary>Motivo da recusa, em ingles como o resto das mensagens. <c>null</c> no sucesso.</summary>
        public string? Message { get; set; }

        /// <summary>Nome da sala, ja aparado. Vazio quando a criacao foi recusada.</summary>
        public string Room { get; set; } = string.Empty;

        /// <summary>Ja havia uma sala com este nome.</summary>
        public bool AlreadyExisted { get; set; }

        public static CreateRoomResponse Rejected(string message) =>
            new() { Success = false, Message = message, Room = string.Empty };
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
