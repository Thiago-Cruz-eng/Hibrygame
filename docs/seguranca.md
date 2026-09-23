# Segurança — Hibrygame

Controles implementados, o que cada um cobre e o que **ainda precisa ser feito no ambiente** antes
de um deploy real.

Referência: OWASP Top 10 2021, OWASP API Security Top 10 2023 e OWASP ASVS 4.0. A auditoria que
originou este documento foi aplicada em **2026-09-23**; o histórico da mudança de contrato está em
[FRONTEND_CHANGES.md](./FRONTEND_CHANGES.md).

> **Este arquivo não é uma lista de desejos.** O que está em "controles implementados" está no
> código e coberto por teste. O que está em "checklist de produção" é configuração de ambiente, e
> **nenhum item dele está feito** — nem poderia, porque depende do domínio, do cofre e da
> infraestrutura de quem publicar.

---

## Checklist de produção

Ordem de importância. O primeiro item, sozinho, derruba todos os outros se for ignorado.

| # | Item | Como | Se faltar |
|---|---|---|---|
| 1 | **`Jwt__Key` fora do repositório** | Variável de ambiente ou cofre. Mínimo de 32 bytes, aleatórios | A chave versionada está no histórico do git: qualquer pessoa assina um token com `sub` e papel arbitrários. **A aplicação recusa subir com ela fora de Development** |
| 2 | **Mongo com autenticação e TLS** | `Mongo__ConnectionString` com usuário, senha e `tls=true`, vinda de cofre | Hoje a conexão padrão é `mongodb://localhost:27017`, sem credencial: qualquer um na mesma rede lê e escreve a base inteira |
| 3 | **`Cors__AllowedOrigins`** | Array com a origem real do front, e só ela | O padrão é `http://localhost:3000`, e o navegador do usuário real recusa toda chamada |
| 4 | **`AllowedHosts`** | O domínio real da API, no lugar de `"*"` | Curinga aceita qualquer cabeçalho `Host`, o que abre envenenamento de Host em URLs absolutas montadas a partir dele |
| 5 | **HTTPS de verdade** | Certificado válido no servidor ou no proxy à frente | `UseHsts` e `UseHttpsRedirection` só entram fora de Development, mas não criam certificado nenhum |
| 6 | **`RateLimiting`** | Revisar `AuthPermitPerMinute` (20) e `GlobalPermitPerMinute` (300) para o volume esperado | Valor apertado demais derruba usuário legítimo; folgado demais não barra força bruta |
| 7 | **IP real atrás de proxy** | `ForwardedHeaders` configurado com a rede do proxy | O limite de requisições particiona por `RemoteIpAddress`: atrás de proxy sem isso, **todos os clientes dividem a mesma partição** e o teto vira um teto global |
| 8 | **Rotação de segredo** | Procedimento escrito para trocar `Jwt:Key` | Trocar a chave invalida todo access token emitido (até 60 min de incômodo) e **não** invalida refresh token, que é verificado por PBKDF2 contra o banco. Planeje na ordem: trocar a chave, avisar, deixar os access tokens expirarem |
| 9 | **Índices do Mongo** | Conferir no log da subida que os três foram criados | O índice único de `User.Email` falha se já houver e-mail duplicado — a aplicação sobe assim mesmo e o log diz o que fazer |
| 10 | **Log de segurança monitorado** | Ver a lista abaixo | Os eventos são registrados, mas registro que ninguém lê não é detecção |

### Eventos de segurança a monitorar

Todos em `Warning` ou `Information`, com template estruturado. Nenhum deles registra e-mail, senha
ou token.

| Mensagem | Nível | O que significa quando se repete |
|---|---|---|
| `Failed login attempt for unknown account` | Warning | Alguém percorrendo uma lista de e-mails. Volume alto do mesmo IP é enumeração de contas |
| `Failed login attempt for user {UserId}` | Warning | Força bruta concentrada numa conta que **existe** |
| `Failed password change attempt for user {UserId}` | Warning | Tentativa de tomada de conta a partir de uma sessão aberta |
| `Refresh token reuse detected for user {UserId}` | Warning | Um refresh token já gasto voltou. Ou o cliente repetiu a chamada, ou o valor vazou — a sessão é derrubada nos dois casos |
| `Invalid refresh token presented for user {UserId}` | Warning | Palpite de token, ou cliente com estado velho |
| `User {CallerId} tried to modify/delete user {TargetId}, who outranks them` | Warning | Tentativa de escalada de privilégio por quem já está autenticado |
| `User {TargetId} deleted by {CallerId}` | Information | Remoção física não deixa outro rastro. É o registro que uma investigação procura primeiro |
| `Password changed for user {UserId}` | Information | Correlacionar com a revogação das sessões |

---

## Controles implementados

### A01 / API1 / API5 — Controle de acesso quebrado

| Controle | Onde |
|---|---|
| Alvo de `change-password` é sempre o claim `sub`; `userId` e `modifiedBy` do corpo são ignorados | `UserController.ChangePassword`, `ChangePasswordUseCase` |
| `PUT /users/{id}` recusa alterar quem está acima do nível do chamador e conceder papel acima dele | `UpdateUserUseCase.UpdateAsync` |
| `DELETE /users/{id}` recusa apagar quem está acima do nível do chamador | `DeleteUserUseCase.DeleteAsync` |
| `GET /users/{id}` só a própria conta ou nível ≥ `adm`, com **404** na recusa (não confirma existência) | `UserController.GetUser` |
| Auditoria (`CreatedBy`, `ModifiedBy`) sempre do token, nunca do corpo | `UserController`, casos de uso |
| Autorização por policy hierárquica, nunca `[Authorize(Roles = ...)]` | `AuthorizationComposition`, `MinimumRoleHandler` |
| `sub` do token conferido contra o `userId` do corpo nos endpoints de `/validation` | `ValidationController.IsCallerAuthorizedFor` |

### A02 — Falhas criptográficas

| Controle | Onde |
|---|---|
| Senha e refresh token com PBKDF2-SHA256, 100.000 iterações, salt de 16 B por registro, comparação em tempo fixo | `SecureHashingService` |
| Access token gravado como resumo SHA-256 na coleção `Validation`, nunca em claro | `TokenDigest`, `ValidationService` |
| Refresh token de 64 B de `RandomNumberGenerator`, entregue uma única vez | `TokenService.CreateRefreshToken` |
| JWT restrito a HS256, com assinatura e expiração obrigatórias | `JwtComposition` |

### A03 — Injeção

| Controle | Onde |
|---|---|
| Consultas por expressão LINQ tipada, traduzidas pelo driver — não há concatenação de filtro | `GenericRepository` |
| Nome de sala restrito a `^[\p{L}\p{N} _-]{1,64}$` antes de virar chave de grupo, texto de tela e linha de log | `ChessHub.RoomNamePattern` |
| Todo parâmetro de log que seja `string` vinda de HTTP — corpo, rota ou **claim** — sanitizado antes de entrar em log (CWE-117) | `LogSanitizer`, aplicado em `ValidationService`, `UpdateUserUseCase` e `DeleteUserUseCase` |
| Limites de tamanho em todo campo de texto de request | DTOs de `UseCases/Dto/Request/` |

### A04 / API4 — Desenho inseguro e consumo de recursos

| Controle | Onde |
|---|---|
| Teto de 500 salas no processo e de 5 salas pendentes por usuário | `ChessHub.MaxRooms`, `MaxPendingRoomsPerUser` |
| Sala de partida iniciada é descartada quando esvazia; sala pendente abandonada é recuperada | `ChessHub.DiscardIfSpent`, `ReclaimAbandonedRooms` |
| Corpo de requisição limitado a 1 MB | `Program.cs` (`ConfigureKestrel`) |
| Mensagem de hub limitada a 32 KB | `WebComposition.AddWebLayer` |
| Autoridade do servidor: as seis checagens de `MakeMove` | `ChessHub.MakeMove` / `ApplyMove` |

### A05 — Configuração insegura

| Controle | Onde |
|---|---|
| Cabeçalhos de segurança em toda resposta, inclusive 401/403/429 | `SecurityHeaders` |
| HSTS de um ano com subdomínios, fora de Development | `Program.cs` |
| CORS por configuração, com origem explícita (exigido por `AllowCredentials` do SignalR) | `WebComposition` |
| `EnableDetailedErrors = false` no SignalR: exceção não descreve o servidor para o cliente | `WebComposition` |
| Swagger apenas em Development | `Program.cs` |
| A aplicação **recusa subir** fora de Development com a chave JWT versionada | `JwtComposition.AddJwtSettings` |

### A07 / API2 — Falha de identificação e autenticação

| Controle | Onde |
|---|---|
| Limite de requisições por IP em `/login`, `/register`, `/refresh-token` e `/users/change-password` | `RateLimitingComposition` |
| Senha de 8 a 128 caracteres no cadastro e na troca | `CreateUserRequest`, `RegisterRequest`, `ChangePasswordRequest` |
| Login não distingue e-mail inexistente de senha errada, **nem na mensagem nem no tempo** | `LoginAsyncUseCase` |
| Refresh token rotativo, com cadeia `ReplacedByTokenId` | `RefreshTokenUseCase` |
| Reuso de token revogado revoga todas as sessões ativas do usuário | `RefreshTokenUseCase.HandleNoActiveMatchAsync` |
| Troca de senha revoga todos os refresh tokens do usuário | `ChangePasswordUseCase` |
| Remoção de usuário revoga os tokens e apaga as validações dele | `DeleteUserUseCase` |
| `ClockSkew = TimeSpan.Zero`: expiração é exata | `JwtComposition` |

### A09 — Falha de registro e monitoramento

Ver a tabela de eventos acima. Nenhum registro inclui senha, e-mail ou token.

---

## Limitações conhecidas, e por quê

| Limitação | Efeito | Por que não foi resolvido aqui |
|---|---|---|
| **Access token não é revogável** | Um token emitido vale até expirar, no máximo 60 min — inclusive depois de troca de senha ou remoção do usuário | Exigiria lista de invalidação consultada a cada request, o que troca o custo de uma verificação local por uma ida ao banco em **toda** chamada. O `jti` já existe no token para quando essa decisão for tomada |
| **Estado do hub em processo** | Duas instâncias não compartilham sala, e os tetos de sala valem por instância | É DT-09; escalar exige backplane Redis **e** mover o estado para fora do processo |
| **Sem transação no Mongo** | Remover um usuário grava em três coleções sem atomicidade; falha no meio deixa credencial órfã | A ordem foi escolhida para que o meio-caminho seja seguro: o usuário sai primeiro, e credencial órfã não autentica ninguém |
| **Limite de requisições por IP, em memória** | NAT compartilha partição; reinício zera as janelas | Particionar por conta exigiria identificar antes de autenticar. IP é a única identidade disponível no ponto certo |
| **`Validation` continua sendo autorização paralela** | Superfície a mais, redundante com a autoridade do hub | Há decisão humana pendente sobre o subdomínio continuar existindo. Ver `docs/debito-tecnico.md` |
