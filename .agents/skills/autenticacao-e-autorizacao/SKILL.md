---
name: autenticacao-e-autorizacao
description: >
  Autenticação e autorização do Orchestrator: JWT HS256 com claims, refresh token rotativo
  hasheado com PBKDF2 e cadeia de revogação, hierarquia de papéis em português com
  MinimumRoleHandler e policies Role:X, JWT em query string apenas para /chesshub, e
  defesa em profundidade nos endpoints de validação, limite de requisições nos endpoints de
  credencial, detecção de reuso de refresh token e regras de alçada em PUT/DELETE/GET de usuário.
  Use ao proteger endpoint ou hub, mexer em login, refresh, troca de senha, claim, policy, papel,
  hash de senha, TokenService, SecureHashingService, rate limiting ou configuração de JwtBearer.
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
  Composition/JwtComposition.cs         AddJwtBearer + a guarda da chave de desenvolvimento
  Composition/AuthorizationComposition.cs  as 5 policies + o handler
  Composition/RateLimitingComposition.cs   teto por IP nos endpoints de credencial
  Composition/SecurityHeaders.cs           cabeçalhos de segurança em toda resposta
  Program.cs                     índice da composição + ordem do pipeline
```

O mapa completo de controles, o checklist de produção e as limitações que permanecem estão em
[`docs/seguranca.md`](../../../docs/seguranca.md). Leia antes de mexer em qualquer coisa deste
arquivo.

## Access token

`TokenService.CreateAccessToken(user)` → `AccessTokenResult(Token, ExpiresAt)`.

- HS256 (`SecurityAlgorithms.HmacSha256`) sobre `JwtSettings.Key` em UTF-8.
- Validade: `ExpiresMinutes` (60 por padrão).
- Claims: `sub` (Id do usuário), `email`, `name`, `ClaimTypes.Role` (papel em português),
  `jti` (Guid novo por token).
- Validação em `JwtComposition`: lifetime, audience, issuer, signing key,
  `ClockSkew = TimeSpan.Zero` (expiração é exata, sem tolerância de 5 min),
  `ValidAlgorithms = [HmacSha256]`, `RequireExpirationTime = true` e `RequireSignedTokens = true`.
  Os três últimos fecham a família de "confusão de algoritmo" e o `alg: none`: sem eles, o
  validador aceita qualquer algoritmo que a chave suporte, e um token **sem** `exp` passa pela
  validação de tempo por não ter tempo a validar.
- **Não é revogável.** Emitido, vale até expirar — inclusive depois de troca de senha ou remoção do
  usuário. É a DT-26, com o `jti` já emitido como gancho. Não invente uma blacklist sem antes ler o
  item: ela custa uma ida ao banco em toda requisição autenticada.

Ao adicionar claim: adicione em `CreateAccessToken` **e** cubra em `TokenServiceTests`, que decodifica
o payload por Base64 em vez de usar o handler — isso mantém o teste independente de versão do
`Microsoft.IdentityModel.JsonWebTokens`. Mantenha esse estilo.

## Refresh token

`TokenService.CreateRefreshToken(user)` → `RefreshTokenIssueResult(RawToken, Token)`.

- 64 bytes aleatórios (`RandomNumberGenerator.GetBytes(64)`) em Base64 — o **raw** vai para o
  cliente e nunca é persistido.
- No banco fica `RefreshToken.Create(userId, hash, salt, expiresAt)`: hash PBKDF2 + salt.
  Validade `RefreshTokenDays` (30).
- **Rotação obrigatória** (`RefreshTokenUseCase`): busca os tokens do usuário, **filtra os ativos
  antes de qualquer PBKDF2**, encontra o que casa por `_hashingService.Verify(raw, hash, salt)`,
  então `matchingToken.Revoke("Rotated", novoToken.Id)`, grava a revogação e salva o novo. O antigo
  fica na cadeia via `ReplacedByTokenId`.
- `IsActive => RevokedAt is null && !IsExpired`. `Revoke` é idempotente (sai se já revogado) — e
  isso importa: a primeira revogação é a evidência de por que aquela sessão terminou, e uma segunda
  chamada apagaria data e motivo originais.

Três consequências de projeto:

1. **Verificar refresh token é O(n) nos tokens do usuário** — não há índice possível sobre hash
   com salt por registro. Por isso o caminho feliz percorre **só os ativos**: os revogados se
   acumulam a cada rotação, e cada teste custa 100.000 iterações de PBKDF2. Os revogados só são
   percorridos quando nenhum ativo casou, que é o caminho da detecção de reuso — e aí o custo se
   justifica.
2. **Reuso de token rotacionado derruba a cadeia inteira.** Se nenhum ativo casar mas um
   **revogado** casar, é replay: `LogWarning("Refresh token reuse detected for user {UserId}")`,
   todos os tokens ativos do usuário recebem `Revoke("Reuse detected")`, e a resposta é o genérico
   `"Invalid refresh token"`. Não dá para distinguir "cliente repetiu a chamada" de "alguém
   copiou", e o custo dos dois enganos é assimétrico: pedir login de novo incomoda; deixar o
   ladrão renovar por 30 dias é a conta perdida. Consequência para o cliente: duas abas renovando
   ao mesmo tempo caem nesta regra — o refresh precisa ser serializado no front.
3. **Palpite errado não derruba nada.** Token que não casa nem com ativo nem com revogado gera
   `LogWarning("Invalid refresh token presented for user {UserId}")` e mais nada. Se derrubasse a
   sessão, qualquer um encerraria a sessão de qualquer outro mandando lixo neste endpoint.

**Refresh token é revogado também fora do refresh:** trocar a senha
(`ChangePasswordUseCase`, motivo `"Password changed"`) e remover o usuário
(`DeleteUserUseCase`, motivo `"User deleted"`) revogam todos os ativos daquele usuário. É o que faz
"troquei a senha" significar "as outras sessões morreram".

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

## Alçada: a policy não é suficiente sozinha

A policy responde "o chamador é ao menos X?". Ela **não** sabe sobre quem ele está agindo nem qual
papel ele está pedindo. As duas perguntas que faltam são regra de negócio, e a divisão é esta:

| Onde | O quê | Por quê ali |
|---|---|---|
| Controller | extrair `sub` e o nível do claim de papel (`TryGetCallerRoleLevel`) | só o controller conhece HTTP |
| Controller (`CreateUser`) | papel pedido ≤ nível do chamador | não precisa ler o banco |
| Caso de uso (`Update`, `Delete`) | papel **atual do alvo** ≤ nível do chamador, e papel pedido ≤ nível do chamador | precisa ler o usuário alvo |

Assinaturas: `UpdateAsync(id, req, RoleLevel callerLevel, string callerId)` e
`DeleteAsync(id, RoleLevel callerLevel, string callerId)`. Mensagens (em inglês, como o resto):
`"Cannot modify a user with a role above your own."`, `"Cannot assign a role above your own."`,
`"Cannot delete a user with a role above your own."`.

Papel gravado que `RoleHierarchy` não reconhece **não** conta como nível — mesma leitura de
`MinimumRoleHandler`. Tratá-lo como "acima de todos" travaria a correção do próprio registro.

`GET /users/{id}`: só a própria conta (`sub == id`) ou nível ≥ `Admin`. A recusa é **404, não
403** — 403 confirmaria que aquele id existe, e confirmar existência é metade do trabalho de quem
sonda.

**Identidade e auditoria nunca vêm do corpo.** `CreatedBy`, `ModifiedBy` e o id do usuário alvo
saem do claim `sub`. Os campos equivalentes continuam nos DTOs, sem `[Required]`, apenas para não
quebrar o cliente atual — e são ignorados. Auditoria que aceita o autor informado pelo próprio
autor não é auditoria.

## Limite de requisições

`Composition/RateLimitingComposition.cs`. Sem teto, o PBKDF2 de 100.000 iterações do login é uma
faca de dois gumes: encarece a tentativa para o atacante **e** para o servidor, que paga o custo de
cada palpite.

- política nomeada `RateLimitingComposition.AuthPolicyName` (`"auth"`): janela fixa por IP remoto,
  `RateLimiting:AuthPermitPerMinute` (padrão 20), `QueueLimit = 0`;
- `GlobalLimiter` por IP, `RateLimiting:GlobalPermitPerMinute` (padrão 300);
- recusa: `429`, corpo `{ "success": false, "message": "Too many requests" }`, cabeçalho
  `Retry-After`;
- `app.UseRateLimiter()` **depois** de `UseCors` (para o 429 chegar ao JavaScript com os cabeçalhos
  que o navegador exige) e **antes** de `UseAuthentication` (validar JWT custa criptografia, e uma
  enxurrada não deve pagar esse custo para ser recusada).

**Endpoint novo que receba credencial tem de declarar
`[EnableRateLimiting(RateLimitingComposition.AuthPolicyName)]`.** Nada faz isso por convenção, e
esquecer não quebra nada visivelmente — só deixa a porta sem teto.

Em `appsettings.Development.json` os tetos são folgados (200/3000) porque a suíte E2E do KrockSide
roda a API em Development e faz dezenas de logins em segundos.

`ResolveClientKey` particiona por `RemoteIpAddress`. Atrás de proxy isso é o IP **do proxy** — ver
DT-27 e o item 7 do checklist de `docs/seguranca.md`.

## Guarda da chave de assinatura

`AddJwtSettings(configuration, isDevelopment)` derruba a subida em dois casos: chave com menos de
32 bytes (HS256 exige 256 bits) e, **fora de Development**, chave igual a
`JwtComposition.DevelopmentKeyPlaceholder` — a que está versionada no `appsettings.json`. Ela está
no histórico do git; com ela, qualquer pessoa assina um token com `sub` e papel arbitrários.

O parâmetro `isDevelopment` é obrigatório de propósito: um padrão `true` faria o esquecimento abrir
a porta, que é o oposto do que se quer.

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

- **`Jwt:Key` em `appsettings.json` é valor de desenvolvimento** e precisa sair para variável de
  ambiente (`Jwt__Key`) ou cofre antes de qualquer deploy. A aplicação recusa subir com ele fora de
  Development — ver "Guarda da chave de assinatura" acima.
- **`POST /users` é anônimo e aceita `Role` do corpo** — qualquer um cria `"super adm"` (DT-04).
  Não escreva código novo assumindo que criação de usuário é confiável.
- **`CreatedBy` vem do corpo do request**, não do claim: auditoria de criação é forjável (DT-04).
- **O access token vai para a coleção `Validation` como resumo SHA-256**
  (`UseCases/Security/TokenDigest.cs`), nunca em claro — era a DT-07. O resumo é determinístico e
  sem salt de propósito: o filtro do Mongo compara por igualdade. Não troque por PBKDF2 ali: o
  token já é um segredo de alta entropia, não há dicionário a percorrer, e o custo cairia em toda
  chamada de `/validation`.
- `RequireHttpsMetadata = true` e `UseHttpsRedirection()` estão ativos — cliente HTTP puro falha.
- Não há revogação de access token: uma vez emitido, vale até expirar (DT-26). `Validation`
  **não** é usada como blacklist — ela é autorização de sessão de jogo, não de token.
- **Log de segurança não carrega e-mail, senha nem token.** Os eventos e o que cada um significa
  estão em `docs/seguranca.md`. Texto livre do usuário que entre em log passa por
  `UseCases/LogSanitizer.cs` — sem isso, um valor com `\n` forja linhas de log inteiras (CWE-117).
