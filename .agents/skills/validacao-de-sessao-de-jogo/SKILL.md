---
name: validacao-de-sessao-de-jogo
description: >
  Coleção Validation e o fluxo de sessão de jogo: registro criado no login com o access token,
  sala e cor de peça, os quatro endpoints POST /validation (verify, get, update, can-move), a
  verificação sub-versus-userId como defesa em profundidade, e os defeitos conhecidos (token em
  claro, exceção engolida, método não implementado, filtro com OR permissivo). Use ao mexer em
  Validation, ValidationService, ValidationController, vincular jogador a sala ou cor, ou
  investigar por que uma validação falha.
metadata:
  type: domain-skill
---

# Validação de sessão de jogo

> **Mantendo esta skill**
>
> Este é o subdomínio mais frágil do repositório. Atualize a skill **e**
> `docs/debito-tecnico.md` a cada mudança aqui. Divergência entre skill e código sem decisão
> registrada é escalada para o humano.

## Visão geral do domínio

`Validation` amarra uma **sessão autenticada** a uma **partida**: qual token, qual usuário, em
qual sala, jogando de qual cor, em que dia. A intenção é permitir que o back-end (ou o front)
confirme "este token pode mover peça branca na sala X".

```
Domain/Validation.cs                    entidade (coleção "Validation")
UseCases/ValidationService.cs           implementação + ValidationDto
UseCases/Interfaces/IValidationService.cs
Presentation/ValidationController.cs    quatro endpoints POST
UseCases/Dto/Request/ValidationRequests.cs
UseCases/Dto/Response/ValidationResponses.cs
```

**Importante:** o `ChessHub` **não usa** `IValidationService`. A autoridade sobre movimento vive
inteiramente no hub (as seis checagens do Princípio II). `Validation` é um mecanismo paralelo,
consumido só pelos endpoints REST de validação. Não presuma que ela protege o jogo.

## Entidade

| Campo | Nota |
|---|---|
| `AcessToken` | grafia errada (falta um "c"); guarda o **JWT completo, em claro** |
| `Room` | opcional — `null` no login, preenchido depois |
| `UserId` | string do `Guid` do usuário |
| `PieceColor` | opcional — `null` no login |
| `UserEmail` | e-mail do usuário |
| `DayOfGame` | `DateTime.UtcNow` no default do campo |

`Validation` é o **contraexemplo** das convenções de entidade: setters públicos, sem factory
`Create`, sem auditoria, construída por inicializador de objeto em `ValidationService`. Não copie
esse estilo — ver o Princípio IV da constituição e a skill `casos-de-uso-e-api-http`.

## Ciclo de vida

```
POST /login              → LoginAsyncUseCase chama CreateValidation:
                            AcessToken = access token emitido, Room = null, PieceColor = null
POST /validation/update  → preenche Room e PieceColor da sessão
POST /validation/verify  → "existe validação para este par (userId, token)?"
POST /validation/get     → devolve a validação
POST /validation/can-move→ "este par pode mover esta cor nesta sala?"
```

Cada login cria **um registro novo**: não há update do anterior, nem limpeza, nem expiração. A
coleção cresce um documento por login, para sempre. `DayOfGame` existe mas nada a usa como filtro
(está comentado no código).

## Endpoints e defesa em profundidade

Os quatro são `POST`, exigem `Role:Player` e recebem `UserId` no corpo. Todos passam por:

```csharp
private bool IsCallerAuthorizedFor(string requestUserId, out string token)
{
    var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
    if (string.IsNullOrEmpty(sub) || !string.Equals(sub, requestUserId, StringComparison.Ordinal))
        return false;                       // → Forbid() (403)
    var raw = HttpContext.GetTokenAsync("access_token").GetAwaiter().GetResult();
    if (string.IsNullOrEmpty(raw)) return false;
    token = raw;
    return true;
}
```

Três garantias que isso codifica e que **não podem** ser afrouxadas:

1. o `sub` do claim tem de casar exatamente com o `UserId` do corpo — 403 se divergir;
2. o token comparado é o **token cru da requisição atual**, tirado do `HttpContext`
   (`SaveToken = true`), nunca um token vindo do corpo;
3. método é `POST` — token nunca em query string de REST.

Contratos:

| Endpoint | Request | Response |
|---|---|---|
| `/validation/verify` | `{ UserId }` | `{ Valid: bool }` |
| `/validation/get` | `{ UserId }` | `{ Id, UserId, UserEmail, Room, PieceColor, DayOfGame }` ou `404` |
| `/validation/update/{id}` | `{ UserId, Room, PieceColor, UserEmail }` | `{ Updated: bool }` |
| `/validation/can-move` | `{ UserId, Room, PieceColor, UserEmail, Day }` | `{ CanMove: bool }` |

Detalhes que confundem:

- **`/validation/update/{id}` ignora o `{id}` da rota.** O serviço localiza a validação por
  `(UserId, token)` e atualiza essa. O parâmetro existe na assinatura e não é usado.
- **`UserEmail` e `Day` são recebidos e não filtram nada** — os dois filtros estão comentados
  dentro de `GetValidationCanMove`. Enviar valores errados não muda o resultado.
- `/validation/verify` devolve `200 { Valid: false }`, não erro, quando não encontra.
- `/validation/get` chama `GetValidationByUserToken`, que **lança** quando não encontra
  (`InvalidOperationException` re-lançada) — então na prática esse caminho dá `500`, não o `404`
  que o controller tenta produzir.

## Defeitos conhecidos (leia antes de mexer)

Todos catalogados em `docs/debito-tecnico.md`:

- **DT-07 — token em claro.** O JWT inteiro é persistido e comparado por igualdade de string.
  Vazamento da coleção entrega sessões ativas. O refresh token é hasheado; o access, não. Ao
  corrigir, prefira guardar o `jti` (já existe nos claims) ou o hash do token.
- **DT-05 — três problemas em `ValidationService`:**
  1. todo método é `try { ... } catch { return false; }` **sem logger injetado** — falha de rede,
     filtro errado e "não encontrado" são indistinguíveis, e a exceção nunca é registrada. Viola o
     Princípio III;
  2. `GetValidationByUserIdTokenAndRoom` lança `NotImplementedException` e está na interface;
  3. a sobrecarga `GetValidationCanMove(userId, room, accessToken)` filtra com
     `AcessToken == token || (UserId == userId && Room == room)` — o `||` aceita registro que casa
     **só** pelo token, sem amarrar o usuário. A sobrecarga usada pelo controller (a de 6
     parâmetros) usa `&&` e está correta.
- **DT-08 — grafia:** `AcessToken` → `AccessToken`. Está persistido: renomear exige
  `[BsonElement("AcessToken")]` ou correção pontual dos documentos (não há migration).

## Ao evoluir este subdomínio

Antes de adicionar comportamento, decida a pergunta de fundo (é `[DECISÃO]`, escale ao humano):
**`Validation` deve continuar existindo?** Hoje ela duplica, de forma mais frágil, informação que
o `GameRoom` já tem em memória (quem está em qual sala com qual cor) e que o JWT já carrega (quem
é o usuário). Três caminhos coerentes:

1. **remover** — o hub é a autoridade; os quatro endpoints saem junto;
2. **manter como sessão persistida** de verdade — guardar `jti` em vez do token, expirar junto com
   o access token, e passar o `ChessHub` a consultá-la de fato;
3. **manter só como log de sessão** — sem papel de autorização, e então parar de chamá-la de
   "validação".

Enquanto a decisão não vier: não amplie a superfície, corrija o `||` e o token em claro se tocar
no arquivo, e nunca use `Validation` como fonte de autoridade para movimento — quem decide isso é
o hub.
