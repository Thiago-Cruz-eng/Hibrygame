---
name: autenticacao-e-autorizacao
description: >
  Autenticação e autorização do Orchestrator: JWT HS256 com claims, refresh token rotativo
  hasheado com PBKDF2 e cadeia de revogação, hierarquia de papéis em português com
  MinimumRoleHandler e policies Role:X, JWT em query string apenas para /chesshub, e
  defesa em profundidade nos endpoints de validação. Use ao proteger endpoint ou hub,
  mexer em login, refresh, troca de senha, claim, policy, papel, hash de senha, TokenService,
  SecureHashingService ou configuração de JwtBearer.
metadata:
  type: technical-skill
---

# Autenticação e autorização

> **Mantendo esta skill**
>
> Atualize sempre que o modelo de token, papel ou policy mudar. Refactor que preserva o
> comportamento não exige mudança.

## Visão geral do padrão

Dois tokens, um handler de autorização hierárquico, zero identidade em URL de REST.

```
Orchestrator/
  UseCases/Security/
    TokenService.cs              emite access token (JWT) e refresh token
    SecureHashingService.cs      PBKDF2 para senha e para refresh token
    Authorization/
      RoleHierarchy.cs           enum RoleLevel + mapa string↔nível
      MinimumRoleRequirement.cs  requisito da policy
      MinimumRoleHandler.cs      resolve o maior papel do usuário e compara
  Infra/Settings/JwtSettings.cs  Key, Issuer, Audience, ExpiresMinutes, RefreshTokenDays
  Program.cs                     AddJwtBearer + AddAuthorization (as 5 policies)
```

## Access token

`TokenService.CreateAccessToken(user)` → `AccessTokenResult(Token, ExpiresAt)`.

- HS256 (`SecurityAlgorithms.HmacSha256`) sobre `JwtSettings.Key` em UTF-8.
- Validade: `ExpiresMinutes` (60 por padrão).
- Claims: `sub` (Id do usuário), `email`, `name`, `ClaimTypes.Role` (papel em português),
  `jti` (Guid novo por token).
- Validação em `Program.cs`: lifetime, audience, issuer, signing key, `ClockSkew = TimeSpan.Zero`
  (expiração é exata, sem tolerância de 5 min).

Ao adicionar claim: adicione em `CreateAccessToken` **e** cubra em `TokenServiceTests`, que decodifica
o payload por Base64 em vez de usar o handler — isso mantém o teste independente de versão do
`Microsoft.IdentityModel.JsonWebTokens`. Mantenha esse estilo.

## Refresh token

`TokenService.CreateRefreshToken(user)` → `RefreshTokenIssueResult(RawToken, Token)`.

- 64 bytes aleatórios (`RandomNumberGenerator.GetBytes(64)`) em Base64 — o **raw** vai para o
  cliente e nunca é persistido.
- No banco fica `RefreshToken.Create(userId, hash, salt, expiresAt)`: hash PBKDF2 + salt.
  Validade `RefreshTokenDays` (30).
- **Rotação obrigatória** (`RefreshTokenUseCase`): busca todos os tokens do usuário, encontra o
  que casa por `_hashingService.Verify(raw, hash, salt)`, exige `IsActive`, então
  `matchingToken.Revoke("Rotated", novoToken.Id)`, grava a revogação e salva o novo. O antigo
  fica na cadeia via `ReplacedByTokenId`.
- `IsActive => RevokedAt is null && !IsExpired`. `Revoke` é idempotente (sai se já revogado).

Duas consequências de projeto:

1. **Verificar refresh token é O(n) nos tokens do usuário** — não há índice possível sobre hash
   com salt por registro. Se o volume crescer, a saída é expirar/limpar tokens revogados, não
   trocar por comparação direta (que reintroduziria token em claro).
2. **Reuso de token rotacionado devolve falha, não alerta.** Não há detecção de replay. Se for
   implementar, o gancho natural é `ReplacedByTokenId` (token revogado sendo reusado ⇒ revogar a
   cadeia inteira).

## Hash de senha e de token

`SecureHashingService`: PBKDF2 (`Rfc2898DeriveBytes.Pbkdf2`), SHA256, **100.000 iterações**,
salt 16 bytes, chave 32 bytes, comparação com `CryptographicOperations.FixedTimeEquals`.

```csharp
var (hash, salt) = _hashingService.HashValue(valor);
var ok           = _hashingService.Verify(valor, hash, salt);
```

Serve para senha **e** para refresh token — mesmo serviço, mesmos parâmetros. Nunca compare hash
com `==`; nunca reduza as iterações; nunca reutilize salt entre registros.

## Hierarquia de papéis

```
Player (1) < MainPlayer (2) < TeamLeader (3) < Admin (4) < SuperAdmin (5)
```

Armazenados como string **em português**, e é essa string que vai no claim e no banco:

| `RoleLevel` | string |
|---|---|
| `Player` | `"jogador"` |
| `MainPlayer` | `"jogador principal"` |
| `TeamLeader` | `"lider de time"` |
| `Admin` | `"adm"` |
| `SuperAdmin` | `"super adm"` |

`RoleHierarchy.TryGetLevel(string, out RoleLevel)` faz a leitura (case-insensitive, com `Trim()`);
`NormalizeRole(RoleLevel)` faz a escrita canônica. **Sempre** passe pelos dois — nunca compare
string de papel na mão, nunca grave papel sem normalizar (`CreateUserUseCase` faz isso certo).
Note que `"lider"` não tem acento no mapa: acentuar quebra o lookup.

`MinimumRoleHandler` lê **todos** os claims `ClaimTypes.Role`, resolve o **maior** nível
reconhecido e compara com `requirement.MinimumRole`. Papel desconhecido não conta
(`hasRole` fica `false` se nenhum casar) — logo, papel escrito errado no banco é negação
silenciosa, não erro.

## Policies

Cinco policies registradas em `Program.cs`, uma por nível:
`"Role:Player"`, `"Role:MainPlayer"`, `"Role:TeamLeader"`, `"Role:Admin"`, `"Role:SuperAdmin"`.
São **cumulativas**: `"adm"` passa em `Role:Player`.

```csharp
[Authorize(Policy = "Role:TeamLeader")]     // correto
[Authorize(Roles = "lider de time")]        // PROIBIDO — ignora a hierarquia
```

`[Authorize(Roles = ...)]` cru exige igualdade exata de string e faria um `"super adm"` ser negado.
Nunca use. Endpoint público usa `[AllowAnonymous]` explícito.

`MinimumRoleHandler` é registrado como `Singleton<IAuthorizationHandler>` — ele é stateless;
mantenha assim.

## JWT no SignalR

WebSocket não carrega header custom, então o cliente JS manda o token em `?access_token=`.
O backend aceita **apenas** para o path `/chesshub`:

```csharp
x.Events = new JwtBearerEvents
{
    OnMessageReceived = ctx =>
    {
        var accessToken = ctx.Request.Query["access_token"];
        if (!string.IsNullOrEmpty(accessToken) &&
            ctx.HttpContext.Request.Path.StartsWithSegments("/chesshub"))
            ctx.Token = accessToken;
        return Task.CompletedTask;
    }
};
```

Nunca amplie esse `StartsWithSegments` para outro path. Endpoint REST é header-only: token em
query string vaza em log de servidor, proxy e histórico de browser.

## Defesa em profundidade nos endpoints de validação

`ValidationController` é o padrão a copiar quando o corpo do request carrega um identificador de
usuário:

```csharp
private bool IsCallerAuthorizedFor(string requestUserId, out string token)
{
    var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
    if (string.IsNullOrEmpty(sub) || !string.Equals(sub, requestUserId, StringComparison.Ordinal))
        return false;                       // → Forbid() (403)
    var raw = HttpContext.GetTokenAsync("access_token").GetAwaiter().GetResult();
    ...
}
```

Regras que isso codifica: (1) todos os endpoints são `POST` com `Authorization: Bearer`, nunca
`GET` com token na URL; (2) o `sub` do claim tem de casar com o `userId` do corpo — 403 se
divergir; (3) o token cru é recuperado de `HttpContext`, não do corpo.

`SaveToken = true` no `AddJwtBearer` é o que permite `GetTokenAsync("access_token")`. Não remova.
Em teste de controller, mocke `IAuthenticationService` em `HttpContext.RequestServices` para
`GetTokenAsync` resolver.

## Restrições e armadilhas conhecidas

- **`Jwt:Key` em `appsettings.json` é valor de desenvolvimento** (`replace-with-strong-secret-key`)
  e precisa sair para variável de ambiente/cofre antes de qualquer deploy. Chave HS256 vazada
  permite forjar qualquer papel.
- **`POST /users` é anônimo e aceita `Role` do corpo** — qualquer um cria `"super adm"` (DT-04).
  Não escreva código novo assumindo que criação de usuário é confiável.
- **`CreatedBy` vem do corpo do request**, não do claim: auditoria de criação é forjável (DT-04).
- **O access token é gravado em claro na coleção `Validation`** no login (DT-07). Ao mexer em
  login/validação, prefira `jti` a token inteiro.
- `RequireHttpsMetadata = true` e `UseHttpsRedirection()` estão ativos — cliente HTTP puro falha.
- Não há revogação de access token: uma vez emitido, vale até expirar. `Validation` **não** é
  usada como blacklist.
