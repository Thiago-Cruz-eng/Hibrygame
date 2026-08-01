<!-- SYNC IMPACT REPORT
Version change: none → 1.0.0 (ratificação inicial)
Origem: harness portado de verum-sales-global-backend em 2026-07-31, com princípios
  reescritos para o domínio real deste repositório (engine de xadrez + API SignalR).
Added sections: Princípios I–VII, Technology Stack Standards, Development Workflow, Governance
Removed sections: n/a
Templates updated:
  ✅ .specify/memory/constitution.md — este arquivo
  ✅ .specify/templates/plan-template.md — Constitution Check reescrito para os princípios I–VII
  ✅ .specify/templates/spec-template.md — sem mudança estrutural necessária
  ✅ .specify/templates/tasks-template.md — sem mudança estrutural necessária
Follow-up TODOs:
  - Princípio I tem duas violações herdadas já registradas em docs/debito-tecnico.md
    (DT-01 e DT-02). Elas são débito conhecido, não licença para novas violações.
-->

# Constituição do Hibrygame

Plataforma de xadrez multiplayer: engine de xadrez em C# puro (`Hibrygame/`) + API
ASP.NET Core 8 com SignalR, MongoDB e JWT (`Orchestrator/`).

## Princípios fundamentais

### I. Separação de camadas (NON-NEGOTIABLE)

Duas fronteiras que não podem ser cruzadas:

1. **Engine independente** — `Hibrygame/` é biblioteca de regras de xadrez. NÃO pode
   referenciar `Orchestrator`, ASP.NET Core, MongoDB, SignalR ou qualquer infraestrutura.
   A engine não conhece sala, jogador, conexão nem persistência.
2. **Fluxo de dependência no Orchestrator** — `Domain → UseCases → Infra → Presentation`.
   Nenhum projeto/pasta referencia camada acima de si na cadeia. `Presentation` só fala com
   `UseCases`; `UseCases` só conhece interfaces (`IUserRepositoryNoSql`, `ITokenService`,
   `ISecureHashingService`, `IValidationService`), nunca implementações concretas de `Infra`.

Violação de layering MUST ser apontada em todo code review e MUST NOT ser mergeada.
Duas violações herdadas existem e estão registradas em
[`docs/debito-tecnico.md`](../../docs/debito-tecnico.md) — elas não autorizam novas.

**Racional**: a engine é a única parte com lógica realmente complexa e é testável a custo
zero enquanto não depender de framework. Misturar transporte com regra de xadrez torna todo
teste de regra dependente de hub, conexão e banco.

### II. Autoridade do servidor (NON-NEGOTIABLE)

O cliente é fonte de intenção, nunca de verdade. Em `ChessHub`, todo movimento MUST ser
revalidado no servidor, na ordem:

1. **Partida** — sala existe, `Started == true`, `Finished == false`;
2. **Identidade** — `Context.ConnectionId` está em `gameRoom.Players`;
3. **Turno** — `player.Color == gameRoom.CurrentTurn`;
4. **Posse** — `source.Piece.Color == player.Color`;
5. **Legalidade** — o servidor recalcula `GetPossibleMove` e ignora qualquer lista de
   movimentos enviada pelo cliente;
6. **Auto-xeque** — `Move.MakeMove` desfaz a jogada se o próprio rei terminar em xeque.

Nenhum atalho: é proibido aceitar `possibleMoves`, cor, turno ou identidade vindos do
payload do cliente. Endpoint ou método de hub novo que altere estado de jogo MUST passar
pelas mesmas seis checagens.

**Racional**: o front-end é substituível e não confiável. Toda regra que existir só no
cliente é uma regra que não existe.

### III. Um caso de uso por ação, sem exceção vazando

Cada ação de negócio vive em uma classe `{Ação}{Entidade}UseCase` com um método público.
O caso de uso MUST capturar exceções, logar via `ILogger<T>` e retornar
`{Ação}Response { Success, Message, ... }`. Controller MUST ser fino: recebe DTO, delega ao
caso de uso, traduz `Success` em status HTTP. Controller MUST NOT conter regra de negócio,
nem injetar repositório/serviço de `Infra` diretamente quando existe caso de uso.

Não há MediatR no fluxo de request (o pacote está referenciado, mas não é usado como
pipeline — ver `docs/debito-tecnico.md`). Não introduzir mediator, decorator ou pipeline de
comportamento sem emenda a esta constituição.

**Racional**: exceção que vaza para o controller vira 500 sem mensagem útil; regra em
controller não é testável sem `HttpContext`.

### IV. Entidade protege o próprio estado

Entidade de domínio MUST:

- herdar `BaseEntity` (`Id : Guid`, `[BsonId]`);
- ter setters `protected`/`private`, construtor `protected` sem parâmetros para
  desserialização do Mongo, construtor privado com parâmetros e factory `static Create(...)`;
- expor mutação apenas por método de intenção nomeado (`ChangeName`, `Revoke`,
  `MarkPasswordChangeRequired`) que retorna `this` quando faz sentido encadear;
- registrar auditoria: `CreationInformation` no `Create`, `ModificationInformation` em cada
  mutador;
- declarar a coleção com `[CollectionName(nameof(Entidade))]`.

Modelo anêmico (propriedades públicas com `set` e regra no serviço) MUST NOT ser criado.

**Racional**: sem ORM e sem migrations, a entidade é o único lugar que garante que um
documento gravado é válido.

### V. Teste antes do código, suíte sempre verde

TDD obrigatório para regra de xadrez, caso de uso, entidade e segurança: Red → Green →
Refactor. Exceção apenas para infra pura (registro de DI, atributo, serializer).

Gate de qualidade: `dotnet build` sem erro e `dotnet test` verde. Baseline atual —
**453 aprovados, 1 ignorado** (`KnightTests.GetMovesKnight_AfterOneMove_Correctly`, cuja
aritmética de teste está errada; a engine ainda tem casos de borda no cavalo). Nenhum teste
novo pode ser marcado `Skip` sem justificativa escrita no próprio atributo.

Padrões de teste: xUnit + Moq, AAA. Teste de hub MUST usar nome de sala único
(`$"test-{Guid.NewGuid()}"`) porque o estado do hub é `static`.

**Racional**: a engine tem bugs de borda conhecidos; sem suíte verde não há como distinguir
regressão de bug preexistente.

### VI. Contrato com o front-end é versionado, não quebrado

O contrato do hub `/chesshub` (nomes de métodos invocáveis, nomes de eventos emitidos,
formato de `BoardSnapshot`/`SquareDto`/`PieceDto`) e as rotas HTTP são consumidos por um
front-end. Mudança que quebra contrato MUST:

1. ser registrada em `docs/FRONTEND_CHANGES.md` (criar na primeira mudança) com antes/depois;
2. preferir adição a alteração — campo novo opcional, método novo, evento novo;
3. quando a quebra for inevitável, expor a versão nova em paralelo antes de remover a antiga.

Coordenadas trocadas com o cliente MUST ser algébricas (`"e2"`, `"e4"`). `Row`/`Column` são
detalhe interno e só aparecem no snapshot como informação redundante.

**Racional**: o back-end não controla o deploy do front. Quebra silenciosa de contrato só
aparece em produção.

### VII. Segredo nunca no repositório

`Jwt:Key` em `Orchestrator/appsettings.json` é valor de desenvolvimento
(`replace-with-strong-secret-key`) e MUST ser resolvido por variável de ambiente ou cofre
antes de qualquer deploy. Nenhum segredo real, string de conexão de produção ou token pode
ser commitado. Token MUST NOT viajar em URL de endpoint REST — endpoints de validação são
`POST` com `Authorization: Bearer`. A exceção é o `?access_token=` do SignalR, aceito
**apenas** para o path `/chesshub` (WebSocket não carrega header custom).

**Racional**: chave HS256 vazada permite forjar qualquer identidade e qualquer papel.

## Padrões de stack

| Área | Tecnologia |
|---|---|
| Runtime | .NET 8 / C# 12 (`net8.0`, `Nullable` e `ImplicitUsings` habilitados) |
| Engine | Biblioteca C# própria em `Hibrygame/` — sem dependência de framework |
| API | ASP.NET Core 8, controllers em `Orchestrator/Presentation/`, Swashbuckle 6 |
| Real-time | SignalR — hub único `/chesshub`, estado `static` em processo |
| Banco | MongoDB (`MongoDB.Driver` 3.x), `Guid` serializado como `string`, sem ORM e sem migrations |
| Auth | JWT HS256 (access 60 min) + refresh token rotativo hasheado (30 dias) |
| Autorização | Policies `Role:{Nível}` sobre `MinimumRoleHandler` + `RoleHierarchy` |
| Testes | xUnit 2.4 + Moq 4.20 (sem FluentAssertions) |

Dependência externa nova MUST entrar atrás de interface em `UseCases/Interfaces/` ou
`Infra/Interfaces/`, com a implementação em `Infra/`. Escolha de tecnologia que atravesse
mais de uma camada MUST ser documentada no plano da feature antes da implementação.

## Fluxo de desenvolvimento

- **Branch**: feature pelo Spec Kit nasce como `NNN-slug` (numeração sequencial de
  `.specify/init-options.json`, criada por `speckit.git.feature`); trabalho fora do fluxo Spec Kit
  usa `feat/<slug-curto>` ou `fix/<slug-curto>`. Não commitar direto na `main`.
- **Commit**: mensagem descreve a intenção da mudança.
- **PR**: usa `.github/PULL_REQUEST_TEMPLATE.md`; `dotnet build` e `dotnet test` verdes antes
  do merge; revisor MUST verificar os Princípios I (camadas), II (autoridade do servidor) e
  IV (entidade).
- **Artefatos de build**: `bin/`, `obj/`, `*.dll`, `*.pdb` MUST NOT ser versionados.
- **Encoding**: `.md` e `.cs` em UTF-8 com acentuação preservada. Normalização para ASCII se
  aplica apenas a mensagem de commit.

## Governança

Esta constituição prevalece sobre prática informal e acordo verbal. Emenda exige:

1. proposta escrita com a mudança e o racional;
2. bump de versão conforme a política abaixo;
3. propagação para os templates de `.specify/templates/` afetados;
4. atualização do bloco SYNC IMPACT REPORT no topo deste arquivo.

**Política de versão**:
- `MAJOR` — remoção ou redefinição incompatível de um princípio;
- `MINOR` — princípio ou seção nova, ou orientação materialmente expandida;
- `PATCH` — clarificação, redação, refinamento não semântico.

Desvio inevitável MUST ser documentado na tabela *Complexity Tracking* do plano da feature
com justificativa, e — se for permanente — em `docs/debito-tecnico.md`.

Orientação operacional para agentes de código fica em [`AGENTS.md`](../../AGENTS.md).

**Versão**: 1.0.0 | **Ratificada**: 2026-07-31 | **Última emenda**: 2026-07-31
