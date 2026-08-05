---
name: partida-em-tempo-real-signalr
description: >
  Hub /chesshub e ciclo de vida da partida: criação e entrada em sala, atribuição de cor,
  início de jogo, as seis checagens obrigatórias de MakeMove (partida, identidade, turno,
  posse, legalidade recalculada, auto-xeque), eventos emitidos, formato de BoardSnapshot,
  estado static em processo e como testar hub. Use ao adicionar ou alterar método de hub,
  mexer em ChessHub ou GameRoom, tratar turno, sala, jogador, desconexão, snapshot de
  tabuleiro ou evento de tempo real.
metadata:
  type: domain-skill
---

# Partida em tempo real (SignalR)

> **Mantendo esta skill**
>
> Atualize sempre que o ciclo de vida da partida ou o contrato do hub mudar. Mudança de
> contrato exige também entrada em `docs/FRONTEND_CHANGES.md`, no mesmo PR.

## Visão geral

`Orchestrator/Infra/SignalR/ChessHub.cs` é o único ponto de entrada de jogo. Ele orquestra
`GameRoom` (estado da sala) e a engine `Hibrygame` (regra de xadrez). O hub **não** implementa
regra de xadrez: ele valida quem pode jogar e delega o resto.

```
ChessHub    métodos invocáveis, eventos, DTOs de saída, validação de autoridade
GameRoom    Board, Players, CurrentTurn, Started, Finished — um objeto por sala
```

Registro: `app.MapHub<ChessHub>("/chesshub")` em `Program.cs`. Autorização:
`[Authorize(Policy = "Role:Player")]` na classe — qualquer papel ≥ `"jogador"` conecta.

## Ciclo de vida da sala

```
CreateRoom(room)          → GameRoom novo no dicionário static
JoinRoom(name, room)       → TryAssignColor: 1º jogador = White, 2º = Black, 3º = recusado
StartGame(room)            → exige IsFull (2 jogadores); Board.StartBoard + MakePieceInInitialState
MakeMove(room, from, to)   → valida, aplica, SwitchTurn, emite BoardChanged
LeaveRoom(room) / desconexão → remove jogador, emite PlayerLeft
```

Detalhes que importam:

- **A cor é do `ConnectionId`, não do usuário.** `GameRoom.Players` é
  `ConcurrentDictionary<connectionId, PlayerSlot(Name, Color)>`. Reconectar gera
  `ConnectionId` novo e, portanto, um slot novo — se a sala já tinha 2 jogadores, o jogador
  que caiu não volta para a mesma cor. Não existe reconexão por identidade.
- **`StartGame` é idempotente por `GameRoom.Start()`** (`if (Started) return;`), mas o hub
  emite `GameStarted` de novo e devolve `Success = true`. Chamar duas vezes não reseta o
  tabuleiro.
- **`OnDisconnectedAsync` varre todas as salas** e remove o `ConnectionId` de qualquer uma,
  emitindo `PlayerLeft`. Não remove a sala vazia: salas vazias ficam no dicionário para sempre
  (vazamento lento, aceitável hoje).
- **Fim de partida não existe.** `Finish()` nunca é chamado (DT-13). `Finished` só é lido.

## As seis checagens de `MakeMove` (NON-NEGOTIABLE)

Princípio II da constituição. Qualquer método novo que altere o tabuleiro repete **todas**:

```csharp
if (!Rooms.TryGetValue(room, out var gameRoom) || !gameRoom.Started)   // 1. partida
    return MakeMoveResponse.Failure("Game not started.");
if (gameRoom.Finished)
    return MakeMoveResponse.Failure("Game already finished.");
if (!gameRoom.Players.TryGetValue(Context.ConnectionId, out var player)) // 2. identidade
    return MakeMoveResponse.Failure("You are not in this room.");
if (player.Color != gameRoom.CurrentTurn)                                // 3. turno
    return MakeMoveResponse.Failure("Not your turn.");
// ... parse algébrico ...
if (source.Piece.Color != player.Color)                                  // 4. posse
    return MakeMoveResponse.Failure("That piece is not yours.");
var (possibleMoves, _) = source.Piece.GetPossibleMove(gameRoom.Board, source); // 5. legalidade
if (!possibleMoves.Any(p => p.Row == target.Row && p.Column == target.Column))
    return MakeMoveResponse.Failure("Illegal move.");
var applied = await Move.MakeMove(gameRoom.Board, possibleMoves, target, source); // 6. auto-xeque
if (!applied) return MakeMoveResponse.Failure("Move would leave king in check.");
```

Proibido:

- aceitar `possibleMoves`, cor, turno, `ConnectionId` ou identidade vindos do payload do cliente;
- usar o resultado de uma chamada anterior de `GetPossibleMoves` — o servidor **sempre** recalcula;
- confiar em `Context.User` para decidir a cor: a cor vem do slot da sala, não do JWT.

`GetPossibleMoves` é conveniência de UI. Não autoriza nada e pode ser chamado para qualquer peça
de qualquer cor a qualquer momento (só exige `Started`). Isso é intencional; não "conserte"
adicionando checagem de turno sem antes registrar a mudança de contrato.

## Conversão de coordenada na borda

O hub é a fronteira entre algébrico (cliente) e índice (engine):

```csharp
if (!Position.TryFromAlgebraic(from, out var fromPos) || fromPos is null) return Failure(...);
var source = gameRoom.Board.GetPositionInBoard(fromPos.Row, fromPos.Column);  // casa REAL
```

`TryFromAlgebraic` devolve uma `Position` solta, sem peça. Sempre busque a casa real no tabuleiro
antes de olhar `Piece`. Ver a skill `motor-de-xadrez` para o mapeamento de eixos.

## Eventos emitidos

| Evento | Alvo | Quando |
|---|---|---|
| `PlayerJoined` | `Clients.Group(room)` | após `JoinRoom` bem-sucedido |
| `PlayerLeft` | `Clients.Group(room)` | `LeaveRoom` (sem `Player`) e `OnDisconnectedAsync` (com `Player`) |
| `RoomFull` | `Clients.Caller` | `JoinRoom` em sala com 2 jogadores |
| `RoomNotFound` | `Clients.Caller` | `JoinRoom` em sala inexistente |
| `GameStarted` | `Clients.Group(room)` | após `StartGame` |
| `BoardChanged` | `Clients.Group(room)` | após `MakeMove` aplicado |

Erro de `MakeMove`/`StartGame`/`GetPossibleMoves` **não** gera evento: volta como retorno da
invocação, com `Success = false` e `Message`. Não troque isso por evento de erro sem registrar
em `docs/FRONTEND_CHANGES.md`.

## `BoardSnapshot`

Construído por `BuildSnapshot` a cada `StartGame` e `MakeMove`: as 64 casas na ordem de varredura
de `Positions[row, column]`, cada uma mapeada por `MapSquare` para `SquareDto`
(`File`, `Rank`, `Algebraic`, `Row`, `Column`, `SquareColor`, `Piece?`). `PieceDto` tem `Type`,
`Color`, `IsInCheckState` — todos via `.ToString()` do enum, portanto **PascalCase**
(`"White"`, `"Pawn"`), não os valores `EnumMember` minúsculos.

O snapshot é completo, não incremental: sempre 64 casas. Se o tabuleiro crescer em informação
(histórico, relógio, captura), adicione campo novo no `BoardSnapshot` — não mude o formato de
`Squares`.

Os DTOs são classes aninhadas dentro de `ChessHub` (`ChessHub.BoardSnapshot`, etc.). Se algum
precisar ser reutilizado fora do hub, mova para `UseCases/Dto/Response/` antes — não adicione
`using static`.

## Estado `static` — implicações

```csharp
private static readonly ConcurrentDictionary<string, GameRoom> Rooms = new();
```

- **Instância única.** Duas instâncias da API não compartilham sala. Escalar exige backplane
  Redis **e** mover o estado para fora do processo (DT-09).
- **Sem persistência.** Restart derruba todas as partidas.
- **Compartilhado entre testes.** O dicionário vive no `AppDomain`, então toda a suíte
  `ChessHubTests` divide o mesmo estado. Teste novo **precisa** de nome de sala único:

  ```csharp
  var room = $"test-{Guid.NewGuid()}";
  ```

- `GameRoom` em si **não** é totalmente thread-safe: `Players` é `ConcurrentDictionary`, mas
  `CurrentTurn`/`Started`/`Finished` são campos comuns. Duas jogadas simultâneas na mesma sala
  são possíveis em teoria; na prática o turno alternado serializa. Não adicione caminho que
  escreva estado da sala fora do fluxo de turno sem sincronização.

## Como testar hub

Padrão em `Orchestrator.Test/ChessHubTests.cs`: mockar `IHubCallerClients`, `IGroupManager` e
`HubCallerContext` com Moq, atribuir em `hub.Clients`, `hub.Groups`, `hub.Context`, e verificar
tanto o retorno da invocação quanto o `SendAsync` esperado no grupo. Ver a skill
`estrategia-de-testes` para o esqueleto completo.

Sempre teste o caminho de recusa, não só o de sucesso: cada uma das seis checagens merece um
teste com sua `Message` exata — é o que impede alguém remover uma checagem sem quebrar a suíte.

## Restrições conhecidas

- `CreateRoom.AlreadyExisted` não significa "a sala já existia" e sim "a sala tem jogador"
  (DT-14).
- `JoinRoom` devolve objeto vazio (`JoinRoomResponse.Empty()`) em falha, além de emitir o evento —
  o cliente precisa checar `Color == null`.
- Não há kick, pausa, spectator nem limite de salas.
- Nada liga usuário autenticado a `PlayerSlot`: o `playerName` de `JoinRoom` é string livre e não
  é validada contra o JWT.
