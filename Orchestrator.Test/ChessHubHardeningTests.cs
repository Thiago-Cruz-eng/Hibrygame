using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Orchestrator.Infra.SignalR;
using Xunit;

namespace Orchestrator.Test;

/// <summary>
/// Endurecimento do hub: validação do nome de sala, tetos de criação, identidade do jogador vinda
/// do token e descarte da sala gasta.
///
/// <para>
/// <b>Nome de sala único por teste.</b> O dicionário de salas é <c>static</c> e vive o
/// <c>AppDomain</c> inteiro, então toda a suíte divide o mesmo estado. Os testes de teto também
/// usam um usuário único por teste, pelo mesmo motivo: a contagem é por criador.
/// </para>
/// </summary>
public class ChessHubHardeningTests
{
    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static string NewRoomName() => $"test-{Guid.NewGuid()}";

    private static string NewUserId() => $"user-{Guid.NewGuid()}";

    /// <summary>
    /// Um hub com a infraestrutura do SignalR dublada e, opcionalmente, um usuário autenticado.
    /// </summary>
    /// <param name="userId">Claim <c>sub</c>. <c>null</c> reproduz o hub sem usuário resolvido.</param>
    /// <param name="userName">Claim <c>name</c> — o nome com que o jogador aparece na sala.</param>
    private static (ChessHub Hub, Mock<IClientProxy> GroupProxy, Mock<ISingleClientProxy> CallerProxy)
        CreateHub(string connectionId, string? userId = null, string? userName = null)
    {
        var clients = new Mock<IHubCallerClients>();
        var callerProxy = new Mock<ISingleClientProxy>();
        var groupProxy = new Mock<IClientProxy>();
        var groups = new Mock<IGroupManager>();
        var context = new Mock<HubCallerContext>();

        context.Setup(c => c.ConnectionId).Returns(connectionId);

        if (userId is not null || userName is not null)
        {
            var claims = new List<Claim>();
            if (userId is not null) claims.Add(new Claim(JwtRegisteredClaimNames.Sub, userId));
            if (userName is not null) claims.Add(new Claim("name", userName));
            context.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")));
        }

        clients.Setup(c => c.Caller).Returns(callerProxy.Object);
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(groupProxy.Object);
        clients.Setup(c => c.All).Returns(groupProxy.Object);

        groups.Setup(g => g.AddToGroupAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        groups.Setup(g => g.RemoveFromGroupAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        callerProxy.Setup(p => p.SendCoreAsync(
                It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        groupProxy.Setup(p => p.SendCoreAsync(
                It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var hub = new ChessHub
        {
            Clients = clients.Object,
            Groups = groups.Object,
            Context = context.Object
        };

        return (hub, groupProxy, callerProxy);
    }

    // ---------------------------------------------------------------
    // CreateRoom — nome de sala
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("sala\ncom quebra")]
    [InlineData("sala/com/barra")]
    [InlineData("sala\"com aspas")]
    public async Task CreateRoom_WithAnInvalidName_IsRejected(string invalidName)
    {
        // O nome circula como chave de grupo do SignalR, aparece na tela de todo mundo no lobby e
        // entra em log. Texto livre nesses tres lugares e convite a HTML injetado na interface
        // alheia e a forja de linha de log.
        var (hub, _, _) = CreateHub("conn-name-1", NewUserId());

        var response = await hub.CreateRoom(invalidName);

        Assert.False(response.Success);
        Assert.Equal(string.Empty, response.Room);
        Assert.NotNull(response.Message);
    }

    [Fact]
    public async Task CreateRoom_WithANameLongerThan64_IsRejected()
    {
        var (hub, _, _) = CreateHub("conn-name-2", NewUserId());

        var response = await hub.CreateRoom(new string('a', 65));

        Assert.False(response.Success);
        Assert.Equal(string.Empty, response.Room);
    }

    [Fact]
    public async Task CreateRoom_WithExactly64Characters_IsAccepted()
    {
        // A fronteira que importa: 64 passa, 65 nao.
        var (hub, _, _) = CreateHub("conn-name-3", NewUserId());
        var name = new string('b', 63) + "z";

        var response = await hub.CreateRoom(name);

        Assert.True(response.Success);
        Assert.Equal(name, response.Room);
    }

    [Theory]
    [InlineData("sala da tarde")]
    [InlineData("Sala-Final_2")]
    [InlineData("sala com acentuação")]
    public async Task CreateRoom_WithAnAcceptableName_IsAccepted(string suffix)
    {
        // Letras Unicode entram: acentuacao e outros alfabetos continuam funcionando.
        var (hub, _, _) = CreateHub("conn-name-4", NewUserId());
        var name = $"{suffix} {Guid.NewGuid():N}";

        var response = await hub.CreateRoom(name);

        Assert.True(response.Success);
        Assert.Equal(name, response.Room);
    }

    [Fact]
    public async Task CreateRoom_TrimsTheName()
    {
        // Sem aparar, "sala" e "sala " virariam duas salas visualmente identicas no lobby.
        var (hub, _, _) = CreateHub("conn-name-5", NewUserId());
        var name = NewRoomName();

        var response = await hub.CreateRoom($"  {name}  ");

        Assert.True(response.Success);
        Assert.Equal(name, response.Room);
    }

    [Fact]
    public async Task CreateRoom_TheSameNameTwice_ReportsItAlreadyExisted()
    {
        // Comportamento antigo preservado: recriar uma sala existente nao e erro.
        var (hub, _, _) = CreateHub("conn-name-6", NewUserId());
        var name = NewRoomName();

        await hub.CreateRoom(name);
        var second = await hub.CreateRoom(name);

        Assert.True(second.Success);
        Assert.True(second.AlreadyExisted);
    }

    // ---------------------------------------------------------------
    // CreateRoom — tetos
    // ---------------------------------------------------------------

    [Fact]
    public async Task CreateRoom_MoreThanFivePendingRoomsForTheSameUser_IsRejected()
    {
        // Sem teto, um cliente cria salas em laco e entope o lobby de todo mundo — e cada sala
        // carrega um tabuleiro de 64 casas na memoria do processo.
        var userId = NewUserId();
        var (hub, _, _) = CreateHub("conn-cap-1", userId);

        for (var i = 0; i < 5; i++)
        {
            var created = await hub.CreateRoom(NewRoomName());
            Assert.True(created.Success);
        }

        var sixth = await hub.CreateRoom(NewRoomName());

        Assert.False(sixth.Success);
        Assert.Equal(string.Empty, sixth.Room);
        Assert.Contains("too many rooms", sixth.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateRoom_StartedRoomsDoNotCountTowardsTheCap()
    {
        // O teto e de salas ESPERANDO adversario. Partida em andamento nao ocupa vaga de espera.
        var userId = NewUserId();
        var (creator, _, _) = CreateHub("conn-cap-white", userId);
        var (opponent, _, _) = CreateHub("conn-cap-black", NewUserId());

        // Cinco salas, todas iniciadas: o criador entra numa, o adversario na mesma, e comeca.
        for (var i = 0; i < 5; i++)
        {
            var name = NewRoomName();
            await creator.CreateRoom(name);
            await creator.JoinRoom("ignorado", name, "White");
            await opponent.JoinRoom("ignorado", name, "Black");
            await creator.StartGame(name);
        }

        var extra = await creator.CreateRoom(NewRoomName());

        Assert.True(extra.Success);
    }

    [Fact]
    public async Task CreateRoom_TheCapIsPerUser()
    {
        var first = NewUserId();
        var (hubOne, _, _) = CreateHub("conn-cap-2", first);
        for (var i = 0; i < 5; i++) await hubOne.CreateRoom(NewRoomName());

        var (hubTwo, _, _) = CreateHub("conn-cap-3", NewUserId());
        var response = await hubTwo.CreateRoom(NewRoomName());

        // O teto de um usuario nao pode impedir outro de jogar.
        Assert.True(response.Success);
    }

    // ---------------------------------------------------------------
    // JoinRoom — identidade
    // ---------------------------------------------------------------

    [Fact]
    public async Task JoinRoom_UsesTheNameFromTheToken_NotTheParameter()
    {
        // O playerName era string livre: dava para entrar na sala se apresentando com o nome do
        // adversario.
        var room = NewRoomName();
        var (creator, _, _) = CreateHub("conn-id-1", NewUserId());
        await creator.CreateRoom(room);

        var (player, _, _) = CreateHub("conn-id-2", NewUserId(), userName: "Ana Real");

        var response = await player.JoinRoom("Nome Forjado", room, "White");

        Assert.Equal("Ana Real", response.Player);
    }

    [Fact]
    public async Task JoinRoom_WithoutANameClaim_FallsBackToTheParameter()
    {
        // Token deste servidor sempre traz `name`; a reserva existe para nao quebrar caso ele
        // falte.
        var room = NewRoomName();
        var (creator, _, _) = CreateHub("conn-id-3", NewUserId());
        await creator.CreateRoom(room);

        var (player, _, _) = CreateHub("conn-id-4", NewUserId());

        var response = await player.JoinRoom("Nome do Cliente", room, "White");

        Assert.Equal("Nome do Cliente", response.Player);
    }

    [Fact]
    public async Task JoinRoom_TruncatesAnOverlongName()
    {
        var room = NewRoomName();
        var (creator, _, _) = CreateHub("conn-id-5", NewUserId());
        await creator.CreateRoom(room);

        var (player, _, _) = CreateHub("conn-id-6", NewUserId());

        var response = await player.JoinRoom(new string('n', 200), room, "White");

        Assert.Equal(64, response.Player!.Length);
    }

    [Fact]
    public async Task JoinRoom_WithAnOverlongRoomName_ReportsRoomNotFound()
    {
        var (player, _, callerProxy) = CreateHub("conn-id-7", NewUserId());

        var response = await player.JoinRoom("Ana", new string('r', 300), "White");

        Assert.Null(response.Color);
        callerProxy.Verify(
            p => p.SendCoreAsync("RoomNotFound", It.IsAny<object[]>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // Descarte da sala gasta
    // ---------------------------------------------------------------

    /// <summary>Uma sala iniciada, com os dois jogadores dentro.</summary>
    private static async Task<(string Room, ChessHub White, ChessHub Black)> StartedRoom()
    {
        var room = NewRoomName();
        var (white, _, _) = CreateHub($"conn-w-{Guid.NewGuid()}", NewUserId(), "Branca");
        var (black, _, _) = CreateHub($"conn-b-{Guid.NewGuid()}", NewUserId(), "Preta");

        await white.CreateRoom(room);
        await white.JoinRoom("Branca", room, "White");
        await black.JoinRoom("Preta", room, "Black");
        await white.StartGame(room);

        return (room, white, black);
    }

    [Fact]
    public async Task LeaveRoom_WhenTheLastPlayerOfAStartedGameLeaves_TheRoomIsDiscarded()
    {
        // Antes nenhuma sala saia do dicionario: ele so crescia enquanto o processo vivesse.
        var (room, white, black) = await StartedRoom();

        await white.LeaveRoom(room);
        await black.LeaveRoom(room);

        var rooms = await white.GetPlayersInEachRoom();
        Assert.DoesNotContain(room, rooms.Keys);
    }

    [Fact]
    public async Task LeaveRoom_WhileTheOpponentIsStillThere_KeepsTheRoom()
    {
        var (room, white, _) = await StartedRoom();

        await white.LeaveRoom(room);

        var rooms = await white.GetPlayersInEachRoom();
        Assert.Contains(room, rooms.Keys);
    }

    [Fact]
    public async Task OnDisconnectedAsync_WhenTheLastPlayerOfAStartedGameDrops_TheRoomIsDiscarded()
    {
        var (room, white, black) = await StartedRoom();

        await white.OnDisconnectedAsync(null);
        await black.OnDisconnectedAsync(null);

        var rooms = await white.GetPlayersInEachRoom();
        Assert.DoesNotContain(room, rooms.Keys);
    }

    [Fact]
    public async Task LeaveRoom_FromARoomThatNeverStarted_KeepsTheRoom()
    {
        // Sala criada e ainda vazia e o estado normal entre criar e entrar: apagar ali quebraria
        // o fluxo do lobby. Dessas cuida a recuperacao por carencia em CreateRoom.
        var room = NewRoomName();
        var (host, _, _) = CreateHub("conn-keep-1", NewUserId(), "Anfitriao");

        await host.CreateRoom(room);
        await host.JoinRoom("Anfitriao", room, "White");
        await host.LeaveRoom(room);

        var rooms = await host.GetPlayersInEachRoom();
        Assert.Contains(room, rooms.Keys);
    }
}
