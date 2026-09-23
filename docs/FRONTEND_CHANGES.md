# Contrato de front-end — Hibrygame

Contrato consumido pelo cliente web. **Append-only**: mudança nova entra como entrada nova no
histórico no fim do arquivo; a seção de contrato acima é sempre o estado atual.

Regra do Princípio VI da [constituição](../.specify/memory/constitution.md): quebra de contrato
exige registro aqui, no **mesmo PR** que muda o código, com antes/depois. Preferir adição
(campo opcional, método novo, evento novo) a alteração.

## Autenticação

Todo acesso parte de `POST /login`.

```
POST /login                       (anônimo, 20 req/min por IP)
POST /register                    (anônimo, 20 req/min por IP)
POST /refresh-token               (anônimo, 20 req/min por IP)
POST /users                       Role:Admin
GET  /users/{id}                  Role:Player — só a própria conta, ou nível ≥ adm
PUT  /users/{id}                  Role:TeamLeader — limitado pela alçada do chamador
DELETE /users/{id}                Role:Admin — limitado pela alçada do chamador
POST /users/change-password       Role:Player — sempre a própria conta, 20 req/min por IP
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
| `CreateRoom` | `room: string` | `{ Success, Message, Room, AlreadyExisted }` — `Success: false` quando o nome não serve ou um teto foi atingido |
| `GetAvailableRooms` | — | `string[]` — salas não cheias e não finalizadas |
| `GetPlayersInRoom` | `room` | `int` |
| `GetPlayersInEachRoom` | — | `{ [room]: string[] }` |
| `JoinRoom` | `playerName, room, preferredColor` | `{ ConnectionId, Player, Room, Color, AssignedColor, PreferenceHonoured }` — objeto vazio se a sala não existe ou está cheia. `Player` vem do token, não do argumento |
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

### 2026-09-23 — endurecimento de segurança pré-produção

Auditoria OWASP Top 10 2021 / API Security Top 10 2023 / ASVS aplicada ao backend. O que o
cliente precisa saber, em ordem de impacto:

**1. `429 Too Many Requests` nos endpoints de credencial.** `POST /login`, `POST /register`,
`POST /refresh-token` e `POST /users/change-password` passam a ter teto de requisições por IP —
20 por minuto em produção, 200 em Development (para a suíte E2E não esbarrar). Um teto global de
300/min por IP vale para o resto da API. O corpo do 429 é
`{ "success": false, "message": "Too many requests" }`, com cabeçalho `Retry-After` em segundos.
O cliente deve tratar 429 como "tente de novo depois", nunca como credencial inválida.

**2. `GET /users/{id}` responde `404` para a conta de outra pessoa.** Antes qualquer jogador
autenticado lia qualquer usuário. Agora só a própria conta (claim `sub` igual ao `{id}`) ou um
chamador com nível ≥ `adm`. A recusa é **404, não 403**, de propósito: 403 confirmaria que aquele
id existe. O front só chama `getUser` para a própria sessão, então nada muda na prática.

**3. `POST /users/change-password` ignora `userId` e `modifiedBy` do corpo.** O alvo é sempre o
`sub` do token. Os dois campos continuam aceitos no corpo — não é preciso mudar o cliente —, mas
não têm mais efeito nenhum. Enviar o id de outra pessoa troca a **sua própria** senha.
Consequência nova: **uma troca de senha bem-sucedida revoga todos os refresh tokens do usuário**,
então as outras sessões dele param de renovar. O access token em uso continua valendo até expirar
(máximo 60 min).

**4. `PUT /users/{id}` e `DELETE /users/{id}` respeitam a hierarquia.** Mensagens novas, com
`Success: false`:

- `"Cannot modify a user with a role above your own."`
- `"Cannot assign a role above your own."`
- `"Cannot delete a user with a role above your own."`

`ModifiedBy` do corpo do `PUT` também passou a ser ignorado: a auditoria grava o `sub`.

**5. Senha: mínimo de 8 caracteres, máximo de 128.** Vale em `POST /users`, `POST /register` e no
`newPassword` de `change-password`. Senha curta devolve `400` com os erros de validação do
`[ApiController]`, antes de chegar ao caso de uso. **O login não tem mínimo** — contas antigas com
senha curta continuam entrando, e trocam a senha depois.

**6. Limites de tamanho nos campos de texto.** `name` 100, `email` 254, `role` 32, `room` 64,
`pieceColor` 16, `day` 32, `userId` 64. Corpo de requisição limitado a 1 MB.

**7. `CreateRoom` pode recusar.** A resposta ganhou `Success` e `Message`:

```ts
type CreateRoomResponse = {
  Success: boolean;        // campo novo
  Message: string | null;  // campo novo — motivo da recusa
  Room: string;            // vazio quando Success = false
  AlreadyExisted: boolean;
};
```

Recusa em três situações:

- **nome inválido** — o nome é aparado (`trim`) e precisa casar com `^[\p{L}\p{N} _-]{1,64}$`:
  letras e dígitos de qualquer alfabeto, espaço, `-` e `_`, de 1 a 64 caracteres. Acentuação
  continua funcionando; barra, aspas, `<`, `>` e quebra de linha não;
- **teto por usuário** — no máximo 5 salas suas ainda não iniciadas ao mesmo tempo;
- **teto do servidor** — 500 salas no processo.

O nome devolvido em `Room` é o **aparado**: `"  sala  "` cria a sala `"sala"`. O cliente deve usar
o `Room` da resposta como chave, não o texto que digitou.

**8. `JoinRoom` usa o nome do token.** O `playerName` do argumento só é usado se o token não
trouxer o claim `name` — o que não acontece com token emitido por esta API. O nome é truncado em
64 caracteres. A assinatura não mudou. Antes era string livre, e dava para entrar numa sala se
apresentando com o nome do adversário.

**9. Sala vazia de partida já iniciada some da lista.** Quando o último jogador de uma partida
iniciada (ou terminada) sai, a sala é removida do servidor. Sala criada e nunca iniciada
continua na lista — é o estado normal entre criar e entrar.

**10. Cabeçalhos novos em toda resposta.** `X-Content-Type-Options: nosniff`,
`X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`,
`Permissions-Policy: camera=(), microphone=(), geolocation=()` e
`Content-Security-Policy: default-src 'none'; frame-ancestors 'none'` (exceto em `/swagger`).
`/login`, `/register` e `/refresh-token` respondem também com `Cache-Control: no-store`. Fora de
Development entra `Strict-Transport-Security` com um ano. Nada disso muda o corpo das respostas;
muda o que o navegador permite fazer com elas.

**11. CORS configurável.** A lista de origens passa a vir de `Cors:AllowedOrigins`. Em
desenvolvimento o padrão continua `http://localhost:3000`. Em produção, a origem real do front
precisa ser configurada no servidor, ou o navegador recusa toda chamada.

**12. Reuso de refresh token derruba a sessão.** Apresentar um refresh token já rotacionado revoga
**todos** os tokens ativos daquele usuário e devolve `"Invalid refresh token"`. Um cliente que
repita a mesma chamada de refresh (por exemplo, duas abas renovando ao mesmo tempo) cai nessa
regra e precisa de login novo — vale serializar o refresh no cliente.

**13. Sessões antigas caem uma vez.** O access token deixou de ser gravado em claro na coleção
`Validation`: agora vai o resumo SHA-256. Registros gravados antes do deploy não casam mais com o
filtro, então quem estiver com sessão aberta no momento do deploy precisa autenticar de novo. O
alcance é de no máximo uma validade de access token, 60 minutos.
