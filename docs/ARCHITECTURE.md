# Hibrygame — Architecture Deep Dive

Companion to [README.md](../README.md). Covers request flows, hub state machine, persistence model, security model, and design decisions.

---

## 1. Layer responsibilities

```
┌────────────────────────────────────────────────────────────────────┐
│  Presentation                                                      │
│    Controllers / SignalR Hub — thin orchestration                  │
│    No business rules, no persistence direct                        │
└────────────────────────┬───────────────────────────────────────────┘
                         ↓ depends on
┌────────────────────────────────────────────────────────────────────┐
│  UseCases (Application services)                                   │
│    One class per action: CreateUserUseCase, LoginAsyncUseCase, ... │
│    Orchestrates Domain + Repositories                              │
│    Owns transaction boundaries (none yet — Mongo non-transactional) │
└────────────────────────┬───────────────────────────────────────────┘
                         ↓ depends on
┌────────────────────────────────────────────────────────────────────┐
│  Domain                                                            │
│    Entities, value objects, factories, invariants                  │
│    Pure C# — no Mongo / no Web / no logging                        │
└────────────────────────┬───────────────────────────────────────────┘
                         ↑ implemented by
┌────────────────────────────────────────────────────────────────────┐
│  Infra                                                             │
│    Mongo repos, security helpers, SignalR plumbing                 │
└────────────────────────────────────────────────────────────────────┘
```

`Domain` has no incoming dependencies from `Infra` — interfaces live in `Infra/Interfaces` and use cases inject them. This is technically lower than strict Clean Architecture (where ports would live in Domain or Application), but acceptable for project size.

---

## 2. Request flow — HTTP

### Example: `POST /login`
```
1. JwtBearer middleware            → no auth required ([AllowAnonymous])
2. UserController.LoginUser
3. LoginAsyncUseCase.LoginAsync
   ├─ IUserRepositoryNoSql.FindByFilter(u => u.Email == norm(email))
   ├─ ISecureHashingService.Verify(password, user.PasswordHash, user.Salt)
   ├─ ITokenService.CreateAccessToken(user)      → JWT (HmacSha256)
   ├─ ITokenService.CreateRefreshToken(user)     → raw + RefreshToken entity
   ├─ IRefreshTokenRepositoryNoSql.Save(refreshToken)
   └─ IValidationService.CreateValidation(...)   → persists session row
4. LoginResponse { accessToken, refreshToken, expiresAt, userId, ... }
```

### Example: `POST /validation/verify` (authenticated)
```
1. JwtBearer middleware            → validates JWT, populates User claims
2. AuthZ pipeline                  → checks "Role:Player" policy
3. ValidationController.Verify
   ├─ IsCallerAuthorizedFor(req.UserId)
   │   ├─ sub claim == req.UserId  → else Forbid
   │   └─ HttpContext.GetTokenAsync("access_token") → else Forbid
   └─ IValidationService.GetValidationByUserToken(userId, rawToken)
4. VerifyValidationResponse { valid: bool }
```

---

## 3. Request flow — SignalR

### Connection handshake
```
Client: new HubConnection.WithUrl(/chesshub, accessTokenFactory)
       ↓
SignalR JS client appends ?access_token=<jwt> to the WebSocket URL
       ↓
ASP.NET pipeline:
  1. JwtBearerEvents.OnMessageReceived
       └─ if path.StartsWithSegments("/chesshub") → ctx.Token = query["access_token"]
  2. JWT validated → User claims populated
  3. AuthZ: [Authorize(Policy = "Role:Player")] on ChessHub
       ↓
HubConnection established. Context.ConnectionId issued.
```

### `MakeMove` flow
```
Client: connection.invoke("MakeMove", "room-1", "e2", "e4")
       ↓
ChessHub.MakeMove
  1. Rooms.TryGetValue(room) → GameRoom or fail "Game not started."
  2. gameRoom.Players[Context.ConnectionId] → fail "You are not in this room."
  3. player.Color == gameRoom.CurrentTurn → else "Not your turn."
  4. Position.TryFromAlgebraic(from), TryFromAlgebraic(to) → else "Invalid square notation."
  5. source.Piece != null → else "No piece on '{from}'."
  6. source.Piece.Color == player.Color → else "That piece is not yours."
  7. source.Piece.GetPossibleMove(board, source) → if !contains(to) → "Illegal move."
  8. Move.MakeMove(board, possibleMoves, target, source)
        ├─ apply move (piece swap)
        ├─ IsKingInCheck(board, ownColor)
        │   ├─ find king position
        │   ├─ reset king.IsInCheckState = false
        │   └─ for each opponent piece: piece.GetPossibleMove (sets king.IsInCheckState if reach)
        └─ if isInCheck → rollback + return false → "Move would leave king in check."
  9. gameRoom.SwitchTurn()
  10. Clients.Group(room).SendAsync("BoardChanged", { from, to, byColor, nextTurn, snapshot })
       ↓
MakeMoveResponse { success: true, from, to, nextTurn, snapshot }
```

---

## 4. Hub state machine

```
                  CreateRoom
                       │
                       ▼
              ┌────────────────┐
              │  Empty Room    │
              └────────┬───────┘
                       │ JoinRoom (1st player → White)
                       ▼
              ┌────────────────┐
              │ Waiting (1/2)  │
              └────────┬───────┘
                       │ JoinRoom (2nd player → Black)
                       ▼
              ┌────────────────┐
              │  Full (2/2)    │
              └────────┬───────┘
                       │ StartGame
                       ▼
              ┌────────────────┐
              │  Started       │◀──┐
              │ CurrentTurn=W  │   │
              └────────┬───────┘   │ MakeMove (alternates turn)
                       │           │
                       │ Finish    │
                       ▼           │
              ┌────────────────┐   │
              │  Finished      │   │
              └────────────────┘   │
                                   │
                       └───────────┘
```

### Concurrency
- `ChessHub` uses a single `static ConcurrentDictionary<string, GameRoom>` shared across all connections.
- `GameRoom.Players` is also `ConcurrentDictionary<string, PlayerSlot>`.
- Mutations: `TryAssignColor` and `Start` are not atomic compound ops — race possible if two clients call `StartGame` simultaneously. Acceptable given `Start` is idempotent (guarded by `Started` flag) and `TryAssignColor` checks `IsFull` before adding.

### Per-connection state
- `OnDisconnectedAsync` walks all rooms, removes the disconnecting `ConnectionId` from any room's `Players`, broadcasts `PlayerLeft` to that room.

---

## 5. Persistence

### MongoDB collection mapping
| Entity         | Collection name (via `[CollectionName]`) | Notable indexes (manual) |
|----------------|------------------------------------------|--------------------------|
| `User`         | `User`                                   | unique on `Email` (recommended, not enforced) |
| `RefreshToken` | `RefreshToken`                           | index on `UserId`, `ExpiresAt` |
| `Validation`   | `Validation`                             | index on `UserId`, `AcessToken` |

### ID serialization
- `BaseEntity.Id` is `Guid`, default `Guid.NewGuid()`.
- Program registers `BsonSerializer.RegisterSerializer(new GuidSerializer(BsonType.String))` — Guids stored as strings, not BinData. Easier to query manually.
- `DateTime`/`DateTimeOffset` also stored as strings.

### Repository abstraction
```
IGenericRepositoryNoSql<T>            ← generic CRUD, used by use cases
       ▲
       │ implements
       │
BaseRepositoryNoSql<T>                ← delegates to IGenericRepository
       ▲
       │ inherits
       │
UserRepositoryNoSql, RefreshTokenRepositoryNoSql, ValidationRepositoryNoSql
```

`IGenericRepository` (no generic on the interface itself, only on methods) provides:
- `GetAll<T>(filter, skip, limit, sort)` — list
- `GetFirstOrDefault<T>(filter)` — single
- `GetProjected<T, TDest>(projection, filter)` — server-side projection
- `Save<T>` / `SaveMany<T>` / `ReplaceOne<T>` / `SaveOrReplaceOne<T>`
- `DeleteOne<T>` / `DeleteMany<T>`
- `Update<T>` — typed setter pairs `(Expression<Func<T, object>>, object)[]`
- `HasRecord<T>` / `CountAsync<T>`

Collection name comes from `[CollectionName(nameof(EntityType))]` on the entity class.

---

## 6. Authentication & Authorization

### Token lifecycle
```
POST /login
  ├─ access token  (JWT, HS256, 60 min default)
  └─ refresh token (random 64 bytes base64, hashed + salted in DB, 30 days)

POST /refresh-token  { userId, refreshToken }
  ├─ find user by Guid userId
  ├─ find refresh tokens for user, match hash via SecureHashingService.Verify
  ├─ if matchingToken.IsActive (not revoked, not expired):
  │     ├─ matchingToken.Revoke("Rotated", newToken.Id)
  │     ├─ persist new refresh token
  │     └─ return new access + refresh tokens
  └─ else → 401 "Invalid refresh token"
```

### JWT claims
| Claim              | Value                          |
|--------------------|--------------------------------|
| `sub`              | `user.Id` (Guid)               |
| `email`            | `user.Email`                   |
| `name`             | `user.Name`                    |
| `role` (ClaimTypes)| `user.Role` (normalized string)|
| `jti`              | unique Guid per token          |
| `iss`              | `Jwt:Issuer`                   |
| `aud`              | `Jwt:Audience`                 |
| `exp` / `nbf` / `iat` | standard                    |

### Role hierarchy
Numeric levels — policies require `>=`:
```
Player (1) < MainPlayer (2) < TeamLeader (3) < Admin (4) < SuperAdmin (5)
```

Stored as Portuguese strings (`jogador`, `jogador principal`, `lider de time`, `adm`, `super adm`). `RoleHierarchy.TryGetLevel` is case-insensitive and trim-tolerant.

### Endpoint protection
- `[AllowAnonymous]` — login, refresh-token, user creation
- `[Authorize(Policy = "Role:Player")]` — read user, change password, validation endpoints, chess hub
- `[Authorize(Policy = "Role:TeamLeader")]` — update user
- `[Authorize(Policy = "Role:Admin")]` — delete user

### SignalR auth
- WebSocket can't send custom headers in the JS client → token travels via `?access_token=` query string.
- Server-side: `JwtBearerEvents.OnMessageReceived` extracts it **only for `/chesshub` path** to avoid leaking the pattern to other endpoints.
- The standard `Authorization: Bearer` header is still preferred for HTTP-only endpoints.

### Defense-in-depth (validation endpoints)
- Body carries `userId`.
- Controller compares `userId` against the `sub` claim from the verified JWT.
- Mismatch → `Forbid()` (403). Prevents a valid token from being used to act on a different user's data even if the FE has a bug.

---

## 7. Chess engine internals

### Position
Three concurrent representations on every square:
- Internal `Row`/`Column` (0–7 indices, board layout-specific)
- Algebraic `File` (a-h) / `Rank` (1-8) computed from row/col
- String `Algebraic` (`"e4"`)

Conversion exposed via `Position.FromAlgebraic`, `Position.TryFromAlgebraic`, `Position.ToIndices`.

### Board init
```
Board.StartBoard()         → fills Positions[8,8] with empty Position(r,c)
Board.MakePieceInInitialState() → iterates Positions once,
                                   sets SquareColor (Black/White checker),
                                   sets Piece per DefinePiece(pos)
```

`DefinePiece` uses switch by column:
- `Column == 7` (White back rank): rooks at row 0/7, knights at 1/6, bishops at 2/5, queen at 3, king at 4
- `Column == 6` (White pawns)
- `Column == 1` (Black pawns)
- `Column == 0` (Black back rank, mirror)
- else `null`

### Move calculation
`Piece.GetPossibleMove(board, pos)` per concrete piece supplies a list of `Direction`s and step count. `Move.CalculatePossibleMove` walks each direction up to `steps` squares applying:
- Board-bounds check (`Common.IsInsideTheBoard`)
- Valid move check (`Common.IsValidMove` — square empty or contains enemy)
- Special-cases pawn diagonals (capture only if enemy present)
- Special-cases knight L-pattern (two cardinal + one perpendicular)
- King-detection: if path reaches enemy king, computes escape moves via `VerifyKingMovementationCheck` + helper moves to block

### Check detection
`Move.IsKingInCheck(board, color)`:
1. Find king of `color`
2. Reset `king.IsInCheckState = false`
3. For each opponent piece, call `GetPossibleMove` (side-effect: sets `king.IsInCheckState = true` if path reaches king)
4. Return `king.IsInCheckState`

### Move application
`Move.MakeMove(board, possibleMoves, newPosition, oldPosition)`:
1. Apply piece swap (`newPosition.Piece = oldPosition.Piece; oldPosition.Piece = null`)
2. Call `IsKingInCheck` for the moving player's color
3. If in check → rollback, return false
4. Clear all `HighlightedPosition` flags
5. Set `HasAlreadyOneMove = true` if moved piece is a pawn (used for double-step rule)

---

## 8. Test strategy

| Layer            | Test approach                                      | Count |
|------------------|----------------------------------------------------|-------|
| Engine (`Hibrygame.Test`) | xUnit Facts, exercise `Piece.GetPossibleMove`, `Move.MakeMove`, `Board` setup | 73 + 1 skip |
| Position         | Round-trip algebraic ↔ row/column, edge cases     | (in engine count) |
| GameRoom         | State transitions, color assignment, idempotency  | 25 |
| ChessHub         | Hub methods with mocked `IHubCallerClients`, `IGroupManager`, `HubCallerContext`; unique room names per test for isolation | 37 |
| UseCases         | Mock repos + services, AAA tests for every branch | 106 |
| Security         | TokenService JWT validation, hashing, role policies | 63 |
| Domain           | Factory + fluent mutators, audit info, entity equality | 119 |
| ValidationController | Auth guard (sub claim, token presence), response shapes | 20 |

Total: **453 passing**, 1 skipped (knight edge-case test with mathematically wrong expectation).

Coverage gaps (not unit-testable without integration):
- Concrete `*RepositoryNoSql` classes — need real Mongo or `IMongoCollection<T>` mocks
- `MongoDbContext` / factory — trivial wiring
- Thin controllers (UserController) — pass-through to use cases

---

## 9. Design decisions log

### Why no MediatR
- 8 use cases. Direct DI injection of `XUseCase` into controller is simpler than MediatR's `IRequest<TResponse>` ceremony.
- MediatR is registered (`services.AddMediatR(...)`) but currently unused — could be removed if not adopted.

### Why no factory by country (vs verum-sales-global-backend)
- Single-tenant app, no multi-country split.
- `IServiceFactory.Create<T>()` exists in `Infra/Utils` but is unused in production code paths. Could be removed.

### Why Guid IDs (not Mongo ObjectId)
- Guid generated client-side (or in `BaseEntity` default) → no round-trip needed to know an entity's ID.
- Stored as string in Mongo (via `GuidSerializer(BsonType.String)`) → human-readable, queryable from shell.
- Cost: 36 bytes vs 12 bytes for ObjectId. Acceptable.

### Why `ReferenceHandler.Preserve`
- Object graphs with cycles (e.g., position ↔ piece ↔ ...) could break default System.Text.Json.
- Tradeoff: every JSON payload has `$id`/`$ref` markers, frontend needs handler.
- Alternative: use `[JsonIgnore]` to break cycles. Not done yet.

### Why static hub state (vs Redis backplane)
- Dev simplicity. Single-instance deployment.
- Scaling path: add `services.AddSignalR().AddStackExchangeRedis(...)` + persist `Rooms` in Redis.

### Why two-token JWT (access + refresh)
- Access tokens short-lived (60 min) — limits damage of leak.
- Refresh tokens long-lived (30 days), persisted, revocable. Rotated on every refresh (`ReplacedByTokenId` chain enables detection of refresh-token reuse attacks).

### Why JWT in query string for SignalR
- Browser WebSocket API in JS clients does not let you set headers.
- Query string is the SignalR-official workaround.
- We restrict the workaround to `/chesshub` path only — REST endpoints stay header-only.

### Why body `userId` + claim verification (not just claim)
- The FE doc dictated body-based payload.
- Server treats claim as truth, body as request shape — mismatch returns 403.
- Net effect: same security as claim-only, plus FE schema compatibility.

---

## 10. Glossary

- **AAA** — Arrange / Act / Assert. Test structure convention.
- **AuditableEntity-style** — entities that track `CreationInformation` + `ModificationInformation` for who/when of last write.
- **CurrentTurn** — `GameRoom`'s color whose turn it is. Toggles on every successful `MakeMove`.
- **GameRoom** — in-memory state container per active match. Holds `Board`, player slots, turn, started/finished flags.
- **GenericRepository** — type-parameterized Mongo CRUD layer at `Infra/BaseRepository/`. Implements `IGenericRepository`.
- **MinimumRoleHandler** — `IAuthorizationHandler` evaluating `MinimumRoleRequirement` policies via role claim hierarchy.
- **Snapshot** — frozen view of a `Board` serialized into 64 `SquareDto` entries plus turn/started/finished metadata, sent on `GameStarted` and `BoardChanged`.
- **TokenService** — issues access JWT + refresh token. Refresh tokens are hashed with `ISecureHashingService` before persistence — raw token never stored.
