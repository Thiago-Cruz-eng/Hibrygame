---
name: estrategia-de-testes
description: >
  Como escrever teste neste projeto: xUnit e Moq sem FluentAssertions, TDD obrigatório,
  nomenclatura Metodo_Cenario_Resultado, campo _sut montado no construtor, helpers BuildRequest,
  receita completa de mock de ChessHub (IHubCallerClients, ISingleClientProxy, IGroupManager,
  HubCallerContext) com sala de nome único, mock de IAuthenticationService para controller,
  decodificação de JWT por Base64 e a baseline de 453 aprovados e 1 ignorado. Use antes de
  escrever o primeiro teste de qualquer task, ao criar teste de caso de uso, entidade, engine,
  hub ou controller, mockar repositório ou rodar a suíte.
metadata:
  type: technical-skill
---

# Estratégia de testes

> **Mantendo esta skill**
>
> Atualize sempre que uma receita de teste mudar ou uma nova aparecer. Teste novo que siga as
> receitas aqui não exige mudança.

## Visão geral

TDD obrigatório (Princípio V): Red → Green → Refactor, para regra de xadrez, caso de uso,
entidade e segurança. Exceção apenas para infra pura (registro de DI, atributo, serializer).

Duas suítes, quatro projetos:

| Projeto | Alvo | Baseline |
|---|---|---|
| `Hibrygame.Test` | engine (`BishopTests`, `BoardTests`, `CommonTests`, `KingTests`, `KnightTests`, `PawnTests`, `PositionTests`, `QueenTests`, `RookTests`) | 73 aprovados, 1 ignorado |
| `Orchestrator.Test` | `Domain/`, `UseCases/`, `Security/`, `Presentation/`, `ChessHubTests`, `GameRoomTests` | 380 aprovados |

```bash
dotnet test                       # 453 aprovados, 1 ignorado
dotnet test Hibrygame.Test        # só a engine
dotnet test Orchestrator.Test     # só a API
```

Stack: **xUnit 2.4 + Moq 4.20**. Não há FluentAssertions, AutoFixture, Testcontainers nem banco
real — não introduza nenhum deles sem decisão explícita. `coverlet.collector` está instalado mas
não há gate de cobertura.

Nenhum teste precisa de MongoDB ou Docker: todo repositório é mockado.

## Convenções

- **Nome**: `Metodo_Cenario_Resultado` — `CreateAsync_WhenEmailAlreadyExists_ReturnsFailure`,
  `GetMovesKnight_AfterOneMove_Correctly`.
- **Estrutura**: AAA (Arrange / Act / Assert), com as seções separadas por linha em branco.
- **`_sut`** é o campo do sujeito sob teste, montado no construtor da classe de teste junto com
  os mocks (xUnit cria uma instância por teste, então o estado não vaza entre testes).
- **Helpers privados estáticos** para construir entrada (`BuildRequest(...)` com parâmetros
  default nomeados) e para setup repetido (`SetupNoExistingUser()`). Isso mantém o corpo do teste
  focado no que varia.
- **Blocos comentados** separando seções (`// --- Helpers ---`) são o estilo em uso; mantenha.
- **Asserção dupla**: verifique o retorno **e** o efeito colateral esperado
  (`_repo.Verify(r => r.Save(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once)`).
  Teste que só olha o retorno passa mesmo quando o `Save` desapareceu.
- **Teste do caminho de recusa é obrigatório** para cada validação, com a `Message` exata. É o que
  impede alguém remover uma checagem de autoridade sem quebrar a suíte.
- `Skip` exige justificativa escrita no próprio atributo. Existe **um** hoje
  (`KnightTests.GetMovesKnight_AfterOneMove_Correctly`, DT-11) — não adicione outro.

## Receita: caso de uso

```csharp
public class CreateUserUseCaseTests
{
    private readonly Mock<IUserRepositoryNoSql> _userRepositoryMock;
    private readonly Mock<ISecureHashingService> _hashingServiceMock;
    private readonly Mock<ILogger<CreateUserUseCase>> _loggerMock;
    private readonly CreateUserUseCase _sut;

    public CreateUserUseCaseTests()
    {
        _userRepositoryMock  = new Mock<IUserRepositoryNoSql>();
        _hashingServiceMock  = new Mock<ISecureHashingService>();
        _loggerMock          = new Mock<ILogger<CreateUserUseCase>>();
        _sut = new CreateUserUseCase(
            _userRepositoryMock.Object, _hashingServiceMock.Object, _loggerMock.Object);
    }

    private static CreateUserRequest BuildRequest(
        string email = "user@example.com", string role = "jogador", ...) => new() { ... };

    private void SetupNoExistingUser() =>
        _userRepositoryMock
            .Setup(r => r.FindByFilter(
                It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<User>());
}
```

Pontos que economizam tempo:

- `FindByFilter` recebe `Expression<Func<T, bool>>` **e** `CancellationToken` — o setup precisa dos
  dois `It.IsAny<>`, senão o mock não casa e devolve `null` (que quebra com
  `NullReferenceException` em vez de falhar claramente).
- Mocke `ILogger<T>` sem verificar chamada de log: log não é contrato. Verificar `LogError` deixa
  o teste frágil.
- Para testar o `catch`, faça o mock lançar: `.ThrowsAsync(new Exception("boom"))` e verifique
  `Success == false` com a `Message` genérica.
- Repositório mockado devolve `IEnumerable<T>`; use `Array.Empty<T>()` ou
  `new[] { user }`, não `null`.

## Receita: hub SignalR

O estado do `ChessHub` é `static`, então **isolamento é responsabilidade do teste**:

```csharp
private static string NewRoomName() => $"test-{Guid.NewGuid()}";
```

Nunca use nome fixo (`"sala1"`): a suíte roda em qualquer ordem e o dicionário é compartilhado.

Montagem completa dos mocks (ASP.NET Core 8 — `Clients.Caller` é `ISingleClientProxy`, não
`IClientProxy`):

```csharp
var clientsMock     = new Mock<IHubCallerClients>();
var callerProxyMock = new Mock<ISingleClientProxy>();
var groupProxyMock  = new Mock<IClientProxy>();
var groupsMock      = new Mock<IGroupManager>();
var contextMock     = new Mock<HubCallerContext>();

contextMock.Setup(c => c.ConnectionId).Returns(connectionId);
clientsMock.Setup(c => c.Caller).Returns(callerProxyMock.Object);
clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(groupProxyMock.Object);
clientsMock.Setup(c => c.All).Returns(groupProxyMock.Object);

groupsMock.Setup(g => g.AddToGroupAsync(
    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
groupsMock.Setup(g => g.RemoveFromGroupAsync(
    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

callerProxyMock.Setup(p => p.SendCoreAsync(
    It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
groupProxyMock.Setup(p => p.SendCoreAsync(
    It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

var hub = new ChessHub { Clients = clientsMock.Object, Groups = groupsMock.Object, Context = contextMock.Object };
```

- **`SendAsync` é extension method** e não pode ser mockado: verifique `SendCoreAsync`, que é o que
  ela chama por baixo. Para checar o evento:

  ```csharp
  groupProxyMock.Verify(p => p.SendCoreAsync(
      "BoardChanged", It.IsAny<object[]>(), It.IsAny<CancellationToken>()), Times.Once);
  ```

- Dois jogadores exigem **duas** instâncias de hub, uma por `ConnectionId`, compartilhando a mesma
  sala pelo nome. `CreateHub("conn-white")` e `CreateHub("conn-black")`.
- `GameRoomTests` testa `GameRoom` isolado, sem mock de SignalR — prefira testar regra de sala lá
  e reserve `ChessHubTests` para autoridade, eventos e conversão de coordenada.

## Receita: controller com token

`ValidationController` lê o claim `sub` e chama `HttpContext.GetTokenAsync("access_token")`, que
por baixo resolve `IAuthenticationService` do `RequestServices`:

```csharp
var props = new AuthenticationProperties();
props.StoreTokens(new[] { new AuthenticationToken { Name = "access_token", Value = accessToken } });
var ticket = new AuthenticationTicket(principal, props, "Bearer");

var authServiceMock = new Mock<IAuthenticationService>();
authServiceMock
    .Setup(x => x.AuthenticateAsync(It.IsAny<HttpContext>(), It.IsAny<string?>()))
    .ReturnsAsync(AuthenticateResult.Success(ticket));

var services = new ServiceCollection();
services.AddSingleton(authServiceMock.Object);

var httpContext = new DefaultHttpContext { User = principal, RequestServices = services.BuildServiceProvider() };
controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
```

Sem o mock de `IAuthenticationService`, `GetTokenAsync` lança `InvalidOperationException`.
Cubra os três caminhos: `sub` ausente → `Forbid`; `sub` divergente do corpo → `Forbid`;
token ausente → `Forbid`.

## Receita: JWT

`TokenServiceTests` decodifica o payload **na mão, por Base64**, em vez de usar
`JwtSecurityTokenHandler`. Isso mantém o teste independente da versão de
`Microsoft.IdentityModel.JsonWebTokens` (que muda o nome dos claims entre versões). Mantenha esse
estilo: divida o token por `.`, corrija o padding do Base64Url e leia o JSON.

Para `JwtSettings`, use `Options.Create(new JwtSettings { Key = "...32+ chars...", ... })` — a chave
HS256 precisa de pelo menos 32 caracteres ou o construtor da `SymmetricSecurityKey` falha.

## Receita: engine

Testes da engine são puros: `new Board()`, `StartBoard()`, `MakePieceInInitialState()`, mova peças
atribuindo `board.Positions[row, col].Piece` e verifique o resultado de `GetPossibleMove`.

- Monte o cenário **em índices** (`Row`/`Column`) ou converta com `Position.FromAlgebraic` —
  não misture os dois no mesmo teste sem deixar claro qual eixo está usando. Lembre:
  `Column = 7` é o rank 1 (brancas). Errar isso é a causa do único teste `Skip` do projeto.
- Compare posições por `Row`/`Column`, nunca por referência:
  `Assert.Contains(moves, m => m.Row == 3 && m.Column == 4)`.
- Ao mexer em `Move.CalculatePossibleMove`, rode `dotnet test Hibrygame.Test` **inteiro** e
  inclua `KingTests` — a detecção de xeque depende de efeito colateral do cálculo de movimento
  (ver skill `motor-de-xadrez`).

## Fora de escopo hoje

Sem teste de integração, sem `WebApplicationFactory`/`TestServer`, sem teste de `Program.cs`, sem
teste de repositório contra Mongo real, sem teste de contrato do SignalR ponta a ponta. Se uma
task precisar de qualquer um deles, isso é decisão de arquitetura: escale antes de montar
infraestrutura nova de teste.
