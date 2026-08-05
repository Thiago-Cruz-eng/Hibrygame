# Contrato de front-end — Hibrygame

Contrato consumido pelo cliente web. **Append-only**: mudança nova entra como entrada nova no
histórico no fim do arquivo; a seção de contrato acima é sempre o estado atual.

Regra do Princípio VI da [constituição](../.specify/memory/constitution.md): quebra de contrato
exige registro aqui, no **mesmo PR** que muda o código, com antes/depois. Preferir adição
(campo opcional, método novo, evento novo) a alteração.

## Autenticação

Todo acesso parte de `POST /login`.

```
POST /login                       (anônimo)
POST /refresh-token               (anônimo)
POST /users                       (anônimo — ver DT-04 em docs/debito-tecnico.md)
GET  /users/{id}                  Role:Player
PUT  /users/{id}                  Role:TeamLeader
DELETE /users/{id}                Role:Admin
POST /users/change-password       Role:Player
POST /validation/verify           Role:Player
POST /validation/get              Role:Player
POST /validation/update/{id}      Role:Player
POST /validation/can-move         Role:Player
```

`LoginResponse`: `Success`, `Message`, `AccessToken`, `RefreshToken`, `ExpiresAt`, `Email`,
`UserId`, `Role`, `MustChangePassword`.

- **Access token** — JWT HS256, 60 min. Vai em `Authorization: Bearer {token}` em **todo**
  request REST. Nunca em query string de endpoint REST.
- **Refresh token** — string base64 de 64 bytes. Enviado em `POST /refresh-token` com
  `{ UserId, RefreshToken }`. **É rotativo**: cada refresh invalida o anterior e devolve um par
  novo. O cliente precisa substituir os dois tokens que guardou; reusar o refresh antigo devolve
  `401` com `Success = false`.
- **Papéis** — string em português: `"jogador"`, `"jogador principal"`, `"lider de time"`,
  `"adm"`, `"super adm"`, nessa ordem crescente de privilégio. As policies são cumulativas:
  quem é `"adm"` passa em `Role:Player`.

### `ReferenceHandler.Preserve` nas respostas REST

A API serializa controllers com `ReferenceHandler.Preserve`. Objetos podem vir com `$id` e
referências repetidas como `{"$ref":"1"}`, e listas como `{"$id":"2","$values":[...]}`. O cliente
precisa tratar isso ao desserializar respostas HTTP.

**Isto não vale para o SignalR** — o hub usa o protocolo JSON próprio do SignalR, sem
`Preserve`. Payload de hub é JSON direto.

### Erro

Nenhum endpoint lança exceção para fora: o corpo é sempre o `Response` da operação, com
`Success: false` e `Message`. O status varia (`400`/`401`/`403`/`404`), então **o cliente deve
decidir pelo `Success` do corpo**, não só pelo status.

## SignalR — `/chesshub`

Conexão exige JWT válido com papel ≥ `jogador` (`[Authorize(Policy = "Role:Player")]`).
WebSocket não carrega header custom, então o token vai em query string — aceito pelo servidor
**apenas** para o path `/chesshub`:

```js
const conn = new signalR.HubConnectionBuilder()
  .withUrl("https://localhost:5001/chesshub", { accessTokenFactory: () => accessToken })
  .build();
```

### Métodos invocáveis (cliente → servidor)

| Método | Argumentos | Retorno |
|---|---|---|
| `CreateRoom` | `room: string` | `{ Room, AlreadyExisted }` — ver DT-14: `AlreadyExisted` hoje significa "a sala tem jogador" |
| `GetAvailableRooms` | — | `string[]` — salas não cheias e não finalizadas |
| `GetPlayersInRoom` | `room` | `int` |
| `GetPlayersInEachRoom` | — | `{ [room]: string[] }` |
| `JoinRoom` | `playerName, room` | `{ ConnectionId, Player, Room, Color }` — objeto vazio se a sala não existe ou está cheia |
| `StartGame` | `room` | `{ Success, Message, Snapshot }` |
| `GetBoardSnapshot` | `room` | `BoardSnapshot` ou `null` |
| `GetPossibleMoves` | `room, from` | `{ Success, Message, From, Moves: SquareDto[] }` |
| `MakeMove` | `room, from, to` | `{ Success, Message, From, To, NextTurn, Snapshot }` |
| `LeaveRoom` | `room` | `void` |

`from`/`to` são **algébricos**: `"e2"`, `"e4"`. Minúscula ou maiúscula no arquivo é aceita
(`char.ToLowerInvariant`), fora de `a1..h8` devolve `Success = false`.

### Eventos emitidos (servidor → cliente)

| Evento | Destino | Payload |
|---|---|---|
| `PlayerJoined` | grupo da sala | `{ Room, Player, Color, Players: [{ Name, Color }] }` |
| `PlayerLeft` | grupo da sala | `{ Room, ConnectionId, Player?, Players: [{ Name, Color }] }` |
| `RoomFull` | quem chamou | `string` (mensagem) |
| `RoomNotFound` | quem chamou | `string` (nome da sala) |
| `GameStarted` | grupo da sala | `BoardSnapshot` |
| `BoardChanged` | grupo da sala | `{ From, To, ByColor, NextTurn, Snapshot }` |

`PlayerLeft` é emitido tanto por `LeaveRoom` (sem `Player`) quanto por `OnDisconnectedAsync`
(com `Player`). O cliente deve tolerar a ausência do campo.

### Formatos

```ts
type BoardSnapshot = {
  Room: string;
  CurrentTurn: "White" | "Black";
  Started: boolean;
  Finished: boolean;      // hoje nunca vira true — ver DT-13
  Squares: SquareDto[];   // 64 casas, ordem de varredura de Positions[row, column]
};

type SquareDto = {
  File: string;           // "a".."h"
  Rank: number;           // 1..8
  Algebraic: string;      // "e4"
  Row: number;            // 0..7 — índice interno da engine, redundante
  Column: number;         // 0..7 — índice interno da engine, redundante
  SquareColor: "White" | "Black" | "None";
  Piece: PieceDto | null;
};

type PieceDto = {
  Type: "Pawn" | "Bishop" | "Knight" | "Rook" | "Queen" | "King" | "None";
  Color: "White" | "Black" | "None";
  IsInCheckState: boolean;
};
```

`Type` e `Color` vêm de `.ToString()` do enum C# — **PascalCase**, não os valores `EnumMember`
minúsculos declarados nos enums (que só valem para `Newtonsoft.Json` e hoje têm um valor errado,
DT-10). Não dependa de `"white"`/`"pawn"`.

### Notação do tabuleiro

Mapeamento entre algébrico e índice interno:

```
File  = 'a' + Row        →  Row    = file - 'a'
Rank  = 8 - Column       →  Column = 8 - rank
```

`Column = 7` é a primeira fileira das brancas (rank 1); `Column = 0` é a das pretas (rank 8).
Brancas sempre começam (`GameRoom.CurrentTurn` inicia em `White`).

### Regras que o servidor impõe (o cliente não decide nada)

`MakeMove` rejeita, nesta ordem, com `Success = false` e `Message`:

1. `"Game not started."` — sala inexistente ou `StartGame` não chamado;
2. `"Game already finished."`;
3. `"You are not in this room."` — `ConnectionId` fora de `Players`;
4. `"Not your turn."`;
5. `"Invalid square notation."`;
6. `"No piece on '{from}'."`;
7. `"That piece is not yours."`;
8. `"Illegal move."` — o servidor recalcula os movimentos legais e ignora qualquer lista enviada
   pelo cliente;
9. `"Move would leave king in check."` — a jogada foi aplicada, detectou auto-xeque e foi
   desfeita; o tabuleiro fica intacto.

`GetPossibleMoves` é conveniência de UI (destacar casas), **não** autorização: chamar `MakeMove`
direto é igualmente válido, e o resultado de `GetPossibleMoves` nunca é confiado no `MakeMove`.

### Limitações do jogo hoje

Sem promoção de peão, roque, en passant, xeque-mate/empate declarado, desistência e relógio.
Uma partida não termina sozinha. Ver DT-13.

## Histórico de mudanças

### 2026-07-31 — baseline

Primeira versão do documento. Nenhuma mudança de contrato: registro do estado atual do
`/chesshub` e das rotas HTTP como linha de base para comparações futuras.
