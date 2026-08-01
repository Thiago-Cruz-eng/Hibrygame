# Hibrygame

Multiplayer chess platform. ASP.NET Core 8 + SignalR + MongoDB + JWT.

> **Canonical instructions live in [`AGENTS.md`](../AGENTS.md).** Read it before any task — it
> defines the mandatory reading order, the non-negotiable conventions and the critical areas.
> This file is a quick reference only; where the two differ, `AGENTS.md` wins.

> **Read first:**
> - [AGENTS.md](../AGENTS.md) — canonical instructions for agents
> - [.specify/memory/constitution.md](../.specify/memory/constitution.md) — 7 principles (I and II are non-negotiable)
> - [.agents/skills/](../.agents/skills/) — domain truth, loaded on demand; **precedes patterns inferred from code**
> - [.agents/maps/functional-map.md](../.agents/maps/functional-map.md) — the 4 business contexts
> - [docs/debito-tecnico.md](../docs/debito-tecnico.md) — known debt; check before "fixing" what looks wrong
> - [README.md](../README.md) — full overview
> - [docs/ARCHITECTURE.md](../docs/ARCHITECTURE.md) — request flows, hub state machine, design log
> - [docs/FRONTEND_CHANGES.md](../docs/FRONTEND_CHANGES.md) — FE contract + change history

## Stack
- **Runtime:** .NET 10 (LTS) / C# 14
- **Database:** MongoDB (Guid IDs stored as string)
- **Real-time:** SignalR (`/chesshub`)
- **Auth:** JWT Bearer (HmacSha256)
- **Tests:** xUnit + Moq (539 passing, 0 skipped)

## Run
```bash
dotnet restore
dotnet build
dotnet run --project Orchestrator   # Swagger at https://localhost:5001/swagger
dotnet test                          # 539 tests, 0 skipped
```

MongoDB on `localhost:27017`. Note: `Program.cs` reads `Mongo:ConnectionString` / `Mongo:Database`
but `appsettings.json` declares `HibrygameDatabase:*` — the keys never match, so the app always
falls back to `localhost:27017` / db `Hibrygame` and the config file is decorative (see DT-06 in
[docs/debito-tecnico.md](../docs/debito-tecnico.md)). The test suite needs neither MongoDB nor
Docker — repositories are mocked with Moq.

## Project layout

```
Hibrygame/        # Chess engine — pure C# library
  Logic/          # Board, Position, Piece (Pawn/Knight/Bishop/Rook/Queen/King), Move, Common
Hibrygame.Test/   # Engine tests
Orchestrator/     # ASP.NET Core Web API
  Domain/         # Entities (User, RefreshToken, UserAssignment, Validation, BaseEntity, AuditInformation)
  Infra/
    BaseRepository/  # IGenericRepository + GenericRepository (Mongo CRUD layer)
    Interfaces/      # IUserRepositoryNoSql, IRefreshTokenRepositoryNoSql, IValidationRepositoryNoSql
    Mongo/           # IMongoDbContext + factory
    Repositories/    # Concrete entity repos (User/RefreshToken/Validation)RepositoryNoSql
    SignalR/         # ChessHub + GameRoom
    Settings/        # JwtSettings
    Utils/           # CollectionNameAttribute, ServiceFactory, EnumStringConverter
  Presentation/   # Controllers — UserController, ValidationController
  UseCases/       # Application services (one class per action)
    Dto/{Request,Response}/
    Security/     # TokenService, SecureHashingService, MinimumRoleHandler, RoleHierarchy
  Program.cs
Orchestrator.Test/
docs/
```

## Architecture (clean-ish)

```
Domain → UseCases → Infra → Presentation
```

- **Domain** — entities + factories, no external deps
- **UseCases** — one class per action (`CreateUserUseCase`, `LoginAsyncUseCase`, …); orchestrates Domain + repos
- **Infra** — Mongo repos, SignalR hub, security helpers
- **Presentation** — thin controllers, delegate to use cases
- DI wired in `Program.cs` (no Scrutor / auto-scan)

## Conventions

### Entity
- Inherit `BaseEntity` — `Id : Guid` defaults to `Guid.NewGuid()`, `[BsonId]`
- `protected` setters, `Create(...)` factory, fluent mutators (`return this`)
- Mongo collection via `[CollectionName(nameof(EntityType))]`
- Audit: `CreationInformation` on create, `ModificationInformation` on each mutator

```csharp
[CollectionName(nameof(User))]
public class User : BaseEntity
{
    public string Name { get; protected set; } = null!;
    public CreationInformation CreationInformations { get; protected set; } = null!;
    public ModificationInformation? ModificationInformations { get; protected set; }

    protected User() { }                             // For Mongo deserialization
    private User(string name, string createdBy)       // Internal constructor
    {
        Name = name;
        CreationInformations = new CreationInformation(createdBy);
    }
    public static User Create(string name, string createdBy) => new(name, createdBy);

    public User ChangeName(string name, string modifiedBy)
    {
        Name = name;
        ModificationInformations = new ModificationInformation(modifiedBy);
        return this;
    }
}
```

### Controller
```csharp
[ApiController]
[Authorize(Policy = "Role:Player")]
[Route("entities")]
public class EntityController(EntityUseCase useCase) : ControllerBase
{
    [HttpPost("action")]
    public async Task<IActionResult> Action([FromBody] ActionRequest req)
    {
        var result = await useCase.ExecuteAsync(req);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
```

- Primary constructor for DI (C# 12)
- One use case per action — inject directly, no factory/MediatR
- `[Authorize(Policy = "Role:X")]` at controller or action level

### UseCase
```csharp
public class CreateXUseCase
{
    private readonly IXRepositoryNoSql _repo;
    private readonly ILogger<CreateXUseCase> _logger;

    public CreateXUseCase(IXRepositoryNoSql repo, ILogger<CreateXUseCase> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task<CreateXResponse> CreateAsync(CreateXRequest req)
    {
        try { ... return new CreateXResponse { Success = true, ... }; }
        catch (Exception e)
        {
            _logger.LogError(e, "Error creating X");
            return new CreateXResponse { Success = false, Message = "..." };
        }
    }
}
```

- Constructor injection, no inheritance
- Try/catch with logger, return `Response { Success, Message }` — no exception bubbling to controller

### Repository
```csharp
public class XRepositoryNoSql : BaseRepositoryNoSql<X>, IXRepositoryNoSql
{
    public XRepositoryNoSql(IGenericRepository generic) : base(generic) { }
}
```

- Inherit `BaseRepositoryNoSql<T>` which delegates to `IGenericRepository`
- Only add methods to concrete repo if entity needs custom queries beyond the generic CRUD

### Naming
| Type                    | Pattern                              |
|-------------------------|--------------------------------------|
| Controller              | `{Entity}Controller`                 |
| Use case                | `{Action}{Entity}UseCase`            |
| Service interface       | `I{Entity}Service`                   |
| Repository interface    | `I{Entity}RepositoryNoSql`           |
| Repository class        | `{Entity}RepositoryNoSql`            |
| Domain entity           | inherits `BaseEntity`                |
| DTO request             | `{Action}Request`                    |
| DTO response            | `{Action}Response`                   |
| SignalR hub             | `{Domain}Hub`                        |

## Auth model

### Role hierarchy
```
Player (1) < MainPlayer (2) < TeamLeader (3) < Admin (4) < SuperAdmin (5)
```
Stored as Portuguese strings: `"jogador"`, `"jogador principal"`, `"lider de time"`, `"adm"`, `"super adm"`.

### Policies (in `Program.cs`)
`"Role:Player"`, `"Role:MainPlayer"`, `"Role:TeamLeader"`, `"Role:Admin"`, `"Role:SuperAdmin"` — each requires role `>=` minimum via `MinimumRoleHandler`.

### Tokens
- Access: JWT HS256, 60 min default
- Refresh: random 64-byte base64, **hashed+salted** in DB, 30 days, **rotated on each refresh**, revocable (`ReplacedByTokenId` chain)

### JWT in SignalR
- WebSocket can't carry custom headers → SignalR JS client sends as `?access_token=` query string
- Backend `JwtBearerEvents.OnMessageReceived` reads it **only for `/chesshub` path**
- REST endpoints stay header-only (`Authorization: Bearer ...`)

### Tokens never in URL (REST)
- All validation endpoints are **POST** with `Authorization: Bearer` header
- Server validates `sub` claim == body `userId` (defense in depth → 403 if mismatch)

## Chess engine

### Coordinates (dual)
- Algebraic: `"e4"`, file `'a'..'h'`, rank `1..8`
- Internal: `Row 0..7`, `Column 0..7`
- Mapping: `file = 'a' + row`, `rank = 8 - column`
- Layout: `Column=7` = White back rank (rank 1), `Column=0` = Black back rank (rank 8)

### Position helpers
- `Position.FromAlgebraic("e4")` (throws), `TryFromAlgebraic`, `ToIndices`
- `Position.Algebraic`, `.File`, `.Rank` getters

### Move calculation
- `Piece.GetPossibleMove(board, pos)` → `(List<Position> moves, Piece? trigger)`
- `Move.MakeMove(board, possibleMoves, target, source)` applies + rolls back if own king ends in check
- `Move.IsKingInCheck(board, color)` — finds king, sweeps opponents, returns `king.IsInCheckState` flag

## SignalR `/chesshub`

Requires `[Authorize(Policy = "Role:Player")]`.

### Client invokes
`CreateRoom(room)`, `GetAvailableRooms()`, `GetPlayersInRoom(room)`, `GetPlayersInEachRoom()`, `JoinRoom(playerName, room)`, `StartGame(room)`, `GetBoardSnapshot(room)`, `GetPossibleMoves(room, from)`, `MakeMove(room, from, to)`, `LeaveRoom(room)`.

`from`/`to` are algebraic (`"e2"`, `"e4"`).

### Server emits
`PlayerJoined`, `PlayerLeft`, `RoomFull`, `RoomNotFound`, `GameStarted`, `BoardChanged`.

### Server-enforced rules
1. Turn — `MakeMove` rejects when `player.Color != gameRoom.CurrentTurn`
2. Identity — `ConnectionId` must be in `gameRoom.Players`
3. Posse — only your own pieces
4. Legal moves — server recomputes `GetPossibleMove`, ignores FE assertion
5. Auto-check — moves that expose own king are rejected, board preserved

### State
- `ChessHub` holds static `ConcurrentDictionary<string, GameRoom>` — single-instance only. Add Redis backplane for horizontal scale.
- `OnDisconnectedAsync` removes disconnected player, broadcasts `PlayerLeft`.

## Mongo

- IDs: Guid, stored as string (`GuidSerializer(BsonType.String)` registered globally)
- Collections named via `[CollectionName(nameof(Entity))]` attribute
- `IGenericRepository` provides typed CRUD; entity-specific repos wrap it (`UserRepositoryNoSql : BaseRepositoryNoSql<User>`)
- No transactions yet — single-document atomicity only

## Config (`Orchestrator/appsettings.json`)

```json
{
  "Mongo": {
    "ConnectionString": "mongodb://localhost:27017",
    "Database": "Hibrygame"
  },
  "Jwt": {
    "Key": "32+chars-strong-secret",
    "Issuer": "https://localhost:5001",
    "Audience": "https://localhost:5001",
    "ExpiresMinutes": 60,
    "RefreshTokenDays": 30
  }
}
```

Dev only — production must move `Jwt:Key` to env var / Key Vault.

## Known limitations

Full catalogue with severity, affected files and exit path:
[docs/debito-tecnico.md](../docs/debito-tecnico.md). Check it **before** "fixing" something that
looks wrong — it may be known debt or an item awaiting a human decision (`[DECISÃO]`).
Highlights: `POST /users` is anonymous and accepts any role (DT-04); the access token is stored in
cleartext in the `Validation` collection (DT-07); `change-password` and `PUT /users` don't check who
is asking (DT-16); reconnecting loses the player's seat in the room (DT-21).

The engine bugs that used to be listed here (knight edge cases, `Position` without value equality,
`ValidationService`) were fixed in the 2026-08-01 refactor — see
[docs/refactor-2026-08-01.md](../docs/refactor-2026-08-01.md).


1. Hub state in-process — no horizontal scale without Redis backplane
2. Knight edge-case bug — `GetMovesKnight_AfterOneMove_Correctly` skipped (test math wrong, engine still has edge cases)
3. No promotion / castling / en-passant
4. JSON uses `ReferenceHandler.Preserve` → `$id`/`$ref` markers in payload (FE must handle)
5. JWT key in `appsettings.json` is dev-only

## Test patterns

- xUnit + Moq, AAA structure
- Hub tests: mock `IHubCallerClients`, `IGroupManager`, `HubCallerContext`; use unique room names (`$"test-{Guid.NewGuid()}"`) since hub state is static
- UseCase tests: mock all repos + `ISecureHashingService` / `ITokenService` / `IValidationService`
- TokenService tests: decode JWT payload via Base64 (version-independent of `Microsoft.IdentityModel.JsonWebTokens`)
- Controller tests: mock `IAuthenticationService` in `RequestServices` so `HttpContext.GetTokenAsync` resolves

## Agent harness

```
AGENTS.md                     Canonical instructions — read first, wins over this file
CLAUDE.md                     Pointer + Spec Kit plan block (machine-managed, do not hand-edit)
.specify/                     Spec Kit: constitution, templates, PowerShell scripts, git extension
.agents/skills/               Domain + technical skills (domain truth) and authoring meta-skills
.agents/maps/                 functional-map.md — the 4 business contexts and their dependencies
.agents/context/              discovery-answers.md — inherited constraints and pending decisions
.claude/agents/               8 subagents (see below)
docs/decisions/               ADRs, immutable once created
```

Subagents in `.claude/agents/`: `hibrygame-dotnet-engineer` (surgical implementation, plan before
code), `code-reviewer`, `unit-test-writer`, `regression-checker`, `spec-reviewer`, `doc-generator`,
`spec-feedback`, `feature-orchestrator` (chains the others).

Spec Kit skills: `speckit-specify`, `speckit-clarify`, `speckit-plan`, `speckit-tasks`,
`speckit-analyze`, `speckit-checklist`, `speckit-implement`, `speckit-constitution`,
`speckit-taskstoissues`, plus the git extension (`speckit-git-feature`, `speckit-git-commit`, …).
Scripts are PowerShell and require a `NNN-slug` branch — they fail on `main` by design.

## CI gates

`.github/workflows/dotnet-test.yml` runs restore + build + test on push and PR to `main`.

Before merge:
- [ ] `dotnet build` 0 errors
- [ ] `dotnet test` all green (539 expected pass, 0 skips) and `dotnet build` with 0 warnings
- [ ] Constitution respected — Principles I (layering) and II (server authority)
- [ ] If FE contract changed: update `docs/FRONTEND_CHANGES.md` (append a dated history entry)
- [ ] If debt was created or resolved: update `docs/debito-tecnico.md`
- [ ] If a domain rule changed: update the matching `.agents/skills/{skill}/SKILL.md`
- [ ] If structure or convention changed: update `AGENTS.md` + `README.md` + this file

## What NOT to do

- Don't inject services directly when a `UseCase` exists — controllers go through use cases
- Don't put secrets in `appsettings.json` (Jwt:Key currently dev-only — flag if hardening for prod)
- Don't track `bin/`, `obj/`, `.dll` (already in `.gitignore`)
- Don't add new GET endpoints that take tokens in URL — use POST + `Authorization` header
- Don't bypass `MinimumRoleHandler` — always use `[Authorize(Policy = "Role:X")]`, never raw `[Authorize(Roles = ...)]`
