# Hibrygame

Multiplayer chess platform. ASP.NET Core 8 backend with SignalR real-time gameplay, MongoDB persistence, JWT auth.

## Stack

- **Runtime:** .NET 10 (LTS) / C# 14
- **Database:** MongoDB (Guid IDs stored as string)
- **Real-time:** SignalR (`/chesshub`)
- **Auth:** JWT Bearer (HmacSha256, symmetric key)
- **Tests:** xUnit + Moq
- **Frontend:** React (`http://localhost:3000` — not in this repo)

## Solution layout

```
Hibrygame.sln
├── Hibrygame/                 # Chess engine — pure C# library, no web/db deps
│   └── Logic/                 # Board, Position, Piece (Pawn/Knight/Bishop/Rook/Queen/King), Move, Common
├── Hibrygame.Test/            # Engine tests (146 passing)
├── Orchestrator/              # ASP.NET Core Web API
│   ├── Domain/                # User, RefreshToken, UserAssignment, Validation, AuditInformation, BaseEntity
│   ├── Infra/
│   │   ├── BaseRepository/    # IGenericRepository + impl
│   │   ├── Interfaces/        # I{User,RefreshToken,Validation}RepositoryNoSql
│   │   ├── Mongo/             # IMongoDbContext + factory
│   │   ├── Repositories/      # Mongo-backed entity repos
│   │   ├── SignalR/           # ChessHub + GameRoom
│   │   ├── Settings/          # JwtSettings
│   │   └── Utils/             # ServiceFactory, EnumStringConverter, CollectionNameAttribute
│   ├── Presentation/          # Controllers — UserController, ValidationController
│   ├── UseCases/              # Application services
│   │   ├── Dto/{Request,Response}/
│   │   └── Security/          # TokenService, SecureHashingService, MinimumRoleHandler, RoleHierarchy
│   └── Program.cs
├── Orchestrator.Test/         # 380 tests covering hub, use cases, security, domain, controllers
└── docs/
    ├── FRONTEND_CHANGES.md    # Frontend migration contract
    └── ARCHITECTURE.md        # This file's deep dive sibling
```

## How to run

### Prereqs
- .NET 10 SDK
- MongoDB running locally on `27017` (or set `Mongo:ConnectionString`)

### Run
```bash
dotnet restore
dotnet build
dotnet run --project Orchestrator
# Swagger: https://localhost:5001/swagger
```

### Configuration (`Orchestrator/appsettings.json`)
```json
{
  "Mongo": {
    "ConnectionString": "mongodb://localhost:27017",
    "Database": "Hibrygame"
  },
  "Jwt": {
    "Key": "replace-with-strong-secret-key-32+chars",
    "Issuer": "https://localhost:5001",
    "Audience": "https://localhost:5001",
    "ExpiresMinutes": 60,
    "RefreshTokenDays": 30
  }
}
```

### Tests
```bash
dotnet test
# Hibrygame.Test:    146 pass
# Orchestrator.Test: 380 pass
```

## Architecture

### Clean-ish layers (Orchestrator)
```
Domain  →  UseCases  →  Infra  →  Presentation
                          ↑
                    (services + repos wired in Program.cs)
```

- **Domain:** entities + factories + audit info. No external deps.
- **UseCases:** application services (orchestrate domain + repos). One class per action (`CreateUserUseCase`, `LoginAsyncUseCase`, etc.).
- **Infra:** MongoDB repos, SignalR hub, security helpers, utilities.
- **Presentation:** thin controllers that delegate to use cases.

### Entity conventions
- Inherit `BaseEntity` — `Id : Guid` defaulted to `Guid.NewGuid()`, `[BsonId]`.
- Domain entities expose `protected` setters, instantiation via `Create(...)` factory, mutators are fluent (`return this`).
- Audit: `AuditableEntity`-style — `CreationInformation` set on creation, `ModificationInformation` set on each mutator.
- MongoDB collection name resolved by `[CollectionName(nameof(EntityType))]` attribute on the class.

### Repository pattern
- `IGenericRepository` — typed generic CRUD on any `BaseEntity`.
- `I{Entity}RepositoryNoSql : IGenericRepositoryNoSql<TEntity>` — entity-specific wrapper, exposes `FindByFilter`, `Save`, `Update`, `Delete`, `GetById`.
- Implementations live in `Infra/Repositories/`. Concrete classes inherit `BaseRepositoryNoSql<T>`.

### Dependency injection
All services registered in `Program.cs`:
- MongoDB: `IMongoClient` (singleton), `IMongoDbContext` (singleton), `IGenericRepository` (scoped), per-entity repos (scoped).
- Auth: `ITokenService`, `ISecureHashingService`, `IAuthorizationHandler` → `MinimumRoleHandler` (singleton).
- Use cases: scoped, one class per action.

### Authentication
- **JWT Bearer**, symmetric HmacSha256 with `Jwt:Key`.
- Login flow: `POST /login` → access token (60 min) + refresh token (30 days).
- Refresh: `POST /refresh-token` → rotates refresh token, revokes old one (`ReplacedByTokenId` chain).
- Role hierarchy (`UseCases/Security/Authorization/RoleHierarchy.cs`):
  ```
  Player (1) < MainPlayer (2) < TeamLeader (3) < Admin (4) < SuperAdmin (5)
  ```
- Policies (in `Program.cs`): `"Role:Player"`, `"Role:MainPlayer"`, `"Role:TeamLeader"`, `"Role:Admin"`, `"Role:SuperAdmin"` — each requires `role >= MinimumRoleRequirement`.
- Roles in DB are stored as Portuguese strings: `"jogador"`, `"jogador principal"`, `"lider de time"`, `"adm"`, `"super adm"`.

## HTTP API

### Auth (anonymous)
| Method | Path                          | Body                                | Response                    |
|--------|-------------------------------|-------------------------------------|-----------------------------|
| POST   | `/login`                      | `LoginRequest`                      | `LoginResponse`             |
| POST   | `/refresh-token`              | `RefreshTokenRequest`               | `RefreshTokenResponse`      |
| POST   | `/users`                      | `CreateUserRequest`                 | `CreateUserResponse`        |

### Users (require role policy)
| Method | Path                          | Min role     | Body                  | Response               |
|--------|-------------------------------|--------------|-----------------------|------------------------|
| GET    | `/users/{id}`                 | Player       | —                     | `GetUserResponse`      |
| PUT    | `/users/{id}`                 | TeamLeader   | `UpdateUserRequest`   | `UpdateUserResponse`   |
| DELETE | `/users/{id}`                 | Admin        | —                     | `DeleteUserResponse`   |
| POST   | `/users/change-password`      | Player       | `ChangePasswordRequest` | `ChangePasswordResponse` |

### Validation (require Player, JWT in `Authorization` header)
| Method | Path                          | Body                                                        | Response                                                    |
|--------|-------------------------------|-------------------------------------------------------------|-------------------------------------------------------------|
| POST   | `/validation/verify`          | `{ userId }`                                                | `{ valid: boolean }`                                        |
| POST   | `/validation/get`             | `{ userId }`                                                | `GetValidationResponse` or 404                              |
| POST   | `/validation/update/{id}`     | `{ userId, room, pieceColor, userEmail }`                   | `{ updated: boolean }`                                      |
| POST   | `/validation/can-move`        | `{ userId, room, pieceColor, userEmail, day }`              | `{ canMove: boolean }`                                      |

All require `Authorization: Bearer <jwt>`. Server validates that `sub` claim equals `userId` in body (defense in depth — body mismatch returns 403).

## SignalR — `/chesshub`

Requires JWT. Client sends via `accessTokenFactory` (translated to `?access_token=` query param by SignalR JS client; backend reads it in `JwtBearerEvents.OnMessageReceived` only for `/chesshub` path).

### Methods (client → server)

| Method                                      | Returns                              |
|---------------------------------------------|--------------------------------------|
| `CreateRoom(string room)`                   | `CreateRoomResponse`                 |
| `GetAvailableRooms()`                       | `string[]`                           |
| `GetPlayersInEachRoom()`                    | `Dictionary<string, string[]>`       |
| `GetPlayersInRoom(string room)`             | `int`                                |
| `JoinRoom(string playerName, string room)`  | `JoinRoomResponse`                   |
| `StartGame(string room)`                    | `StartGameResponse`                  |
| `GetBoardSnapshot(string room)`             | `BoardSnapshot?`                     |
| `GetPossibleMoves(string room, string from)` | `PossibleMovesResponse` (from = `"e2"`) |
| `MakeMove(string room, string from, string to)` | `MakeMoveResponse`                |
| `LeaveRoom(string room)`                    | —                                    |

### Events (server → client)

| Event           | Payload                                                                                  |
|-----------------|------------------------------------------------------------------------------------------|
| `PlayerJoined`  | `{ room, player, color, players: [{ name, color }] }`                                    |
| `PlayerLeft`    | `{ room, connectionId, player?, players: [...] }`                                        |
| `RoomFull`      | string                                                                                   |
| `RoomNotFound`  | string                                                                                   |
| `GameStarted`   | `BoardSnapshot`                                                                          |
| `BoardChanged`  | `{ from, to, byColor, nextTurn, snapshot: BoardSnapshot }`                               |

### Game rules enforced server-side
1. Turn — `MakeMove` rejects if `player.Color != gameRoom.CurrentTurn` ("Not your turn.").
2. Identity — caller's `ConnectionId` must match a player slot in the room.
3. Piece ownership — only your color's pieces can move.
4. Legal moves — server recomputes `GetPossibleMove`; FE-supplied destinations are validated.
5. Auto-check — moves that leave own king in check are rejected, board state preserved.

### Data shapes

```ts
type Color = "White" | "Black" | "None";
type PieceType = "Pawn" | "Knight" | "Bishop" | "Rook" | "Queen" | "King" | "None";

interface SquareDto {
  file: string;        // "a".."h"
  rank: number;        // 1..8
  algebraic: string;   // "e4"
  row: number;         // 0..7 (internal)
  column: number;      // 0..7 (internal)
  squareColor: Color;
  piece: PieceDto | null;
}

interface BoardSnapshot {
  room: string;
  currentTurn: Color;
  started: boolean;
  finished: boolean;
  squares: SquareDto[]; // 64
}
```

## Chess engine (`Hibrygame/`)

### Board coordinates — dual notation

| Notation       | Type   | Range   | Origin                                                |
|----------------|--------|---------|-------------------------------------------------------|
| `algebraic`    | string | a1..h8  | Standard chess: file (a-h) left-to-right, rank (1-8) bottom-to-top from White |
| `file`         | char   | a..h    | Algebraic file                                        |
| `rank`         | int    | 1..8    | Algebraic rank                                        |
| `row`          | int    | 0..7    | Internal index (horizontal)                           |
| `column`       | int    | 0..7    | Internal index (vertical)                             |

Mapping:
```
file   = (char)('a' + row)
rank   = 8 - column
row    = file - 'a'
column = 8 - rank
```

Layout: `Column=7` is White's back rank (rank 1), `Column=0` is Black's back rank (rank 8). White pawns on `Column=6` (rank 2).

### Position helpers
- `Position.FromAlgebraic("e4")` → Position, throws on invalid.
- `Position.TryFromAlgebraic("e4", out var pos)` → bool.
- `Position.ToIndices("e4")` → `(row, column)` tuple.
- `Position.Algebraic` getter on any Position.

### Piece logic
- `Piece` abstract base: `Type`, `Color`, `IsInCheckState`, `HasAlreadyOneMove`.
- Concrete pieces (`Pawn`, `Knight`, `Bishop`, `Rook`, `Queen`, `King`) override `GetPossibleMove(board, pos)` returning `(List<Position> possibleMoves, Piece? actualPieceTrigger)`.
- Movement directions in `Hibrygame/Logic/Enums/Direction.cs` (8 compass directions).
- `Move.MakeMove` applies a move, runs `IsKingInCheck` after, rolls back if it would expose own king.

## Naming conventions

| Type                    | Pattern                              | Example                    |
|-------------------------|--------------------------------------|----------------------------|
| Controller              | `{Entity}Controller`                 | `UserController`           |
| Service interface       | `I{Entity}Service`                   | `IValidationService`       |
| Service class           | `{Entity}Service`                    | `ValidationService`        |
| Use case                | `{Action}{Entity}UseCase`            | `CreateUserUseCase`        |
| Repository interface    | `I{Entity}RepositoryNoSql`           | `IUserRepositoryNoSql`     |
| Repository class        | `{Entity}RepositoryNoSql`            | `UserRepositoryNoSql`      |
| Domain entity           | inherits `BaseEntity`                | `User : BaseEntity`        |
| DTO request             | `{Action}Request`                    | `CreateUserRequest`        |
| DTO response            | `{Action}Response`                   | `LoginResponse`            |
| SignalR hub             | `{Domain}Hub`                        | `ChessHub`                 |

## Known limitations

1. **Hub state is in-process.** `ChessHub` uses static `ConcurrentDictionary` — does not scale horizontally without a Redis backplane. OK for single-instance dev.
2. **Knight L-pattern bug.** `Move.cs` knight handling can return fewer moves than expected in edge-adjacent positions. Test `GetMovesKnight_AfterOneMove_Correctly` skipped (expectation in test was geometrically wrong, but engine still has edge cases).
3. **No promotion / castling / en-passant.** Pawn promotion and castling logic not implemented. Pawn `HasAlreadyOneMove` flag set, but no two-square en-passant capture.
4. **`ReferenceHandler.Preserve`** on JSON serialization adds `$id` / `$ref` — FE must handle.
5. **JWT key in `appsettings.json`** — OK for dev. Production must use Azure Key Vault / env vars.

## Documentation index

- [README.md](README.md) — this file
- [docs/FRONTEND_CHANGES.md](docs/FRONTEND_CHANGES.md) — frontend migration contract (hub naming, auth, payloads)
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — deep architecture dive (request flow, hub state machine, persistence)
- [docs/fluxo-de-trabalho.md](docs/fluxo-de-trabalho.md) — como abrir, testar e mergear uma demanda: back+front, só back, só front; onde cada teste mora
- [docs/workflow-cenarios.md](docs/workflow-cenarios.md) — os mesmos fluxos aplicados a cinco demandas realistas, comando a comando, mais a lista completa de quality gates
