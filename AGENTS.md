# AGENTS.md — Hibrygame

Instruções canônicas deste repositório para agentes de código. `CLAUDE.md` e
`.github/copilot-instructions.md` são apenas ponteiros para este arquivo — nenhuma convenção é
duplicada neles. O `CLAUDE.md` da raiz hospeda também o bloco entre
`<!-- SPECKIT START -->` e `<!-- SPECKIT END -->`, que é **gerenciado pelo Spec Kit** e não deve
ser editado à mão.

## Leia antes de agir (ordem obrigatória)

1. **Constituição do projeto — [`.specify/memory/constitution.md`](.specify/memory/constitution.md).**
   Obrigatória antes de qualquer decisão de arquitetura (nova entidade, novo método de hub, nova
   dependência, nova camada). Os princípios **I (Separação de camadas)** e
   **II (Autoridade do servidor)** são NON-NEGOTIABLE: violação não pode ser mergeada.
2. **Skills em [`.agents/skills/`](.agents/skills/) — fonte de verdade do domínio.** São a
   documentação autoritativa das regras deste projeto e **têm precedência sobre padrões
   inferidos do código existente**. Consulte a skill pertinente **antes** de implementar.
3. **Verificação obrigatória antes de criar ou editar arquivo:** confira se existe skill que
   governa o caso — pela **tecnologia/padrão** (persistência, autenticação, testes, SignalR) ou
   pelo **domínio** (motor de xadrez, partida, usuários, validação de sessão). Se houver skill,
   ela manda; o código existente é evidência secundária.
4. **Mapa funcional — [`.agents/maps/functional-map.md`](.agents/maps/functional-map.md)** para
   saber em qual contexto você está mexendo, e
   **[`.agents/context/discovery-answers.md`](.agents/context/discovery-answers.md)** para as
   restrições e decisões transversais já validadas.
5. **Débito técnico — [`docs/debito-tecnico.md`](docs/debito-tecnico.md)** antes de "corrigir"
   algo que parece errado: pode ser desvio já conhecido e deliberado, ou item que exige decisão
   humana.

## Visão geral

Plataforma de xadrez multiplayer. Dois artefatos com papéis bem distintos:

- **`Hibrygame/`** — engine de xadrez em C# puro. Tabuleiro, posições, peças, cálculo de
  movimento e detecção de xeque. Não conhece HTTP, sala, jogador nem banco.
- **`Orchestrator/`** — API ASP.NET Core 8. Autenticação JWT, usuários em MongoDB e o hub
  SignalR `/chesshub` que hospeda as partidas em memória.

Projeto **pessoal retomado após pausa longa**: a engine tem casos de borda conhecidos (cavalo,
peão) e o Orchestrator tem débito herdado já catalogado. Onde a documentação divergir do código,
**o código prevalece** — e a divergência deve ser corrigida na documentação no mesmo PR.

**Escopo do jogo hoje (não presuma paridade com xadrez completo):** não há promoção de peão,
roque, en passant, xeque-mate/empate declarado nem relógio. `GameRoom.Finished` existe e é
respeitado, mas nada no servidor o liga automaticamente — `Finish()` nunca é chamado.

## Stack

| Área | Tecnologia |
|---|---|
| Runtime | .NET 8 / C# 12 (`net8.0`, `Nullable` e `ImplicitUsings` habilitados) |
| Engine | Biblioteca própria `Hibrygame` (namespace raiz `Hibrygame`) |
| API | ASP.NET Core 8, controllers clássicos, Swashbuckle 6 (Swagger só em Development) |
| Real-time | SignalR 1.1 — hub único `/chesshub`, estado `static` em processo |
| Banco | MongoDB (`MongoDB.Driver` 3.6), `Guid` serializado como `string`, sem ORM e sem migrations |
| Auth | JWT HS256 (`Microsoft.AspNetCore.Authentication.JwtBearer` 8) + refresh token rotativo |
| Hash | PBKDF2 (`Rfc2898DeriveBytes`, SHA256, 100.000 iterações, salt 16B, chave 32B) |
| Serialização | `System.Text.Json` com `ReferenceHandler.Preserve` na API; `Newtonsoft.Json` só nos conversores de enum da engine |
| Testes | xUnit 2.4 + Moq 4.20 (sem FluentAssertions) |
| CI | GitHub Actions — `.github/workflows/dotnet-test.yml` (build + test) |

Dependência externa nova entra **atrás de interface**: contrato em `UseCases/Interfaces/` (regra
de aplicação) ou `Infra/Interfaces/` (persistência), implementação em `Infra/`. Nunca SDK de
terceiro instanciado dentro de caso de uso ou controller.

`MediatR` e `Polly` estão referenciados no `.csproj` mas **não estão em uso** no pipeline de
request (`AddShared()` em `ServiceCollectionExtensions` nem é chamado no `Program.cs`). Não
escreva código novo assumindo que existe mediator ou política de resiliência — ver DT-03 em
`docs/debito-tecnico.md`.

## Estrutura do repositório

```
Hibrygame/Logic/            Engine — Board, Position, Piece + 6 peças, Move, Common, Enums/
Hibrygame.Test/Hibrygame/   Testes da engine (73 pass, 1 skip)
Orchestrator/
  Domain/                   User, UserAssignment, RefreshToken, Validation, BaseEntity, AuditInformation
  UseCases/                 Um caso de uso por ação + Dto/{Request,Response}/ + Interfaces/ + Security/
  Infra/                    BaseRepository/ Interfaces/ Mongo/ Repositories/ SignalR/ Settings/ Utils/
  Presentation/             UserController, ValidationController
  Program.cs                Toda a composição de DI, auth, policies, CORS, SignalR
Orchestrator.Test/          Domain/ UseCases/ Security/ Presentation/ + ChessHubTests + GameRoomTests (380 pass)
.specify/                   Constituição + templates + scripts + extensão git do Spec Kit
specs/                      Especificações de feature do Spec Kit (spec.md, plan.md, tasks.md, contracts/)
.agents/                    Skills, mapa funcional e memória de descoberta
docs/                       README de arquitetura, contrato de FE e débito técnico
.claude/                    Agentes, skills e permissões do Claude Code
```

## Convenções de arquitetura (não negociáveis)

- **Engine sem framework** — `Hibrygame/` não referencia `Orchestrator`, ASP.NET, Mongo nem
  SignalR. Regra de xadrez nova entra na engine, com teste na engine; nunca no hub.
  (Herdado: o `.csproj` da engine ainda referencia `Microsoft.AspNetCore.Mvc.Core` e
  `Hosting.Abstractions` sem usar nada deles — DT-01, remover, não ampliar.)
- **Fluxo de dependência** — `Domain → UseCases → Infra → Presentation`, sem referência
  ascendente. `Presentation` fala só com `UseCases`; `UseCases` só com interfaces.
  (Herdado: `Domain` importa `CollectionNameAttribute` de `Infra/Utils` — DT-02.)
- **Autoridade do servidor** — `MakeMove` revalida partida, identidade, turno, posse, legalidade
  (recalculando `GetPossibleMove`) e auto-xeque. Lista de movimentos vinda do cliente é ignorada.
  Método de hub novo que altere o tabuleiro repete as seis checagens.
- **Use case por ação** — `{Ação}{Entidade}UseCase`, injeção por construtor, try/catch com
  `ILogger<T>`, retorno `{Ação}Response { Success, Message, ... }`. Exceção não sobe para o
  controller. Controller traduz `Success` em status HTTP e nada mais.
- **Entidade protege o próprio estado** — `BaseEntity`, setters `protected`, construtor
  `protected` vazio para o Mongo, construtor privado + `static Create(...)`, mutador nomeado
  retornando `this`, auditoria (`CreationInformation` no create,
  `ModificationInformation` em cada mutador) e `[CollectionName(nameof(Entidade))]`.
- **Repositório** — `{Entidade}RepositoryNoSql : BaseRepositoryNoSql<{Entidade}>,
  I{Entidade}RepositoryNoSql`, delegando a `IGenericRepository`. Só adicione método ao repo
  concreto quando a entidade precisar de query além do CRUD genérico.
- **Autorização sempre por policy** — `[Authorize(Policy = "Role:X")]`. `[Authorize(Roles = ...)]`
  cru é proibido: ele ignora a hierarquia do `MinimumRoleHandler`.
- **Coordenadas** — fronteira externa (hub, DTO, log de jogada) usa algébrico (`"e2"`);
  `Row`/`Column` são internos da engine. Converta na borda com
  `Position.TryFromAlgebraic`/`FromAlgebraic`, nunca com aritmética manual espalhada.
- **Sem migration** — mudança de schema é retrocompatível: campo novo é opcional ou
  `[BsonIgnoreIfNull]`. Não existe script de migração.
- **Segredo fora do repositório** — `Jwt:Key` no `appsettings.json` é valor de desenvolvimento.
  Endpoint REST não recebe token em URL; a exceção `?access_token=` vale só para `/chesshub`.

### Áreas críticas (maior risco de regressão)

- **`Move.CalculatePossibleMove`** — função de ~75 linhas com o caso do cavalo tratado por
  desvio dentro do laço de direção, mutando `newPosition.Piece` durante o cálculo. É a origem
  dos casos de borda conhecidos. Mexa com teste antes e rode a suíte da engine inteira.
- **`Move.MakeMove`** — aplica a jogada, detecta auto-xeque e faz rollback manual dos dois
  quadrados. Qualquer campo novo de peça precisa entrar no rollback ou a jogada rejeitada deixa
  estado sujo.
- **`Move.IsKingInCheck`** — depende de efeito colateral: zera `IsInCheckState` do rei e varre
  os oponentes chamando `GetPossibleMove` só para que alguém marque a flag. Alterar o cálculo de
  movimento pode silenciosamente quebrar a detecção de xeque.
- **`ChessHub` (estado `static`)** — `ConcurrentDictionary` de salas compartilhado por todo o
  processo e por toda a suíte de testes. Teste novo usa nome de sala único; feature nova não
  assume isolamento entre requests.
- **`ValidationService`** — engole exceção e retorna `bool` em quase todo método;
  `GetValidationByUserIdTokenAndRoom` lança `NotImplementedException`;
  `GetValidationCanMove` tem sobrecarga com `||` no filtro que aceita token de outro usuário.
  Superfície de segurança frágil: não amplie sem revisar o filtro (DT-05).
- **`UserController.CreateUser`** — `[AllowAnonymous]` e aceita `Role` do corpo do request:
  qualquer anônimo pode criar `super adm`. Não amplie a superfície sem definir autorização
  (DT-04).

## Configuração e variáveis de ambiente

Não há `.env`. A configuração vive em `Orchestrator/appsettings.json`:

- `Jwt:Key` / `Issuer` / `Audience` / `ExpiresMinutes` / `RefreshTokenDays` — lidos via
  `IOptions<JwtSettings>` e diretamente no `Program.cs`.
- **Atenção:** o `Program.cs` lê a conexão do Mongo em `Mongo:ConnectionString` e
  `Mongo:Database`, mas o `appsettings.json` declara `HibrygameDatabase:*`. Hoje o app sempre cai
  no fallback (`mongodb://localhost:27017`, base `Hibrygame`) e o `appsettings` é decorativo.
  Ver DT-06 — ao corrigir, escolha **uma** das duas chaves e atualize `README.md`,
  `docs/ARCHITECTURE.md` e `.claude/CLAUDE.md` no mesmo PR.

MongoDB local em `localhost:27017`. A suíte de testes **não** precisa de Mongo nem de Docker: os
repositórios são mockados com Moq.

## Comandos essenciais

```bash
dotnet restore
dotnet build                                  # 4 projetos, 0 erros esperado
dotnet run --project Orchestrator             # Swagger em https://localhost:5001/swagger
dotnet test                                   # 453 aprovados, 1 ignorado
dotnet test Hibrygame.Test                    # só a engine (73 pass, 1 skip)
dotnet test Orchestrator.Test                 # só a API (380 pass)
```

Não há linter nem formatter configurado (sem `.editorconfig`, sem `dotnet format` no CI). O
quality gate é `dotnet build` + `dotnet test` verdes.

## Convenções transversais

- **Branch e PR** — duas convenções, por origem do trabalho: feature conduzida pelo **Spec Kit**
  nasce como `NNN-slug` (numeração sequencial, criada por `speckit.git.feature`, exigida pelos
  scripts de `.specify/scripts/powershell/` — eles falham em branch fora desse formato); trabalho
  **fora** do fluxo Spec Kit usa `feat/<slug-curto>` ou `fix/<slug-curto>`. Não commitar direto na
  `main`. PR usa `.github/PULL_REQUEST_TEMPLATE.md`; `dotnet build` e `dotnet test` verdes antes do
  merge.
- **Artefato de build nunca versionado** — `bin/`, `obj/`, `*.dll`, `*.pdb` estão no
  `.gitignore`. Se aparecerem no `git status`, é porque já estavam no índice antes do ignore:
  remova do índice (`git rm -r --cached`), não adicione mais.
- **Encoding** — `.md` e `.cs` em UTF-8 com acentuação preservada; nunca normalizar acento para
  ASCII em conteúdo de arquivo. A normalização ASCII se aplica **apenas** a mensagem de commit.
- **Documentação viva** — `README.md` (visão geral e contratos), `docs/ARCHITECTURE.md` (fluxos,
  máquina de estado do hub, log de decisões), `docs/FRONTEND_CHANGES.md` (contrato de FE,
  append-only) e `docs/debito-tecnico.md` são atualizados **no mesmo PR** que muda o código.
- **Débito técnico** — registro central em [`docs/debito-tecnico.md`](docs/debito-tecnico.md),
  com marcação `[DECISÃO]` no que exige definição humana. Item resolvido sai de lá e é citado no
  PR que o resolveu.
- **Nomenclatura**

  | Tipo | Padrão |
  |---|---|
  | Controller | `{Entidade}Controller` |
  | Caso de uso | `{Ação}{Entidade}UseCase` |
  | Interface de serviço | `I{Nome}Service` |
  | Interface de repositório | `I{Entidade}RepositoryNoSql` |
  | Classe de repositório | `{Entidade}RepositoryNoSql` |
  | DTO | `{Ação}Request` / `{Ação}Response` |
  | Hub | `{Domínio}Hub` |
  | Teste | `{Método}_{Cenário}_{Resultado}` |

- **Não adotar sem pedido explícito** — MediatR como pipeline, AutoMapper, FluentAssertions,
  FluentValidation, Redis backplane, Docker/compose, coleção Postman/Insomnia. Nada disso existe
  hoje; introduzir qualquer um é decisão de arquitetura e exige emenda à constituição.

## Estrutura `.agents/`

| Pasta | Papel |
|---|---|
| `.agents/context/` | Memória da descoberta: objetivo, restrições herdadas, decisões transversais validadas e log de decisões humanas (`discovery-answers.md`) |
| `.agents/maps/` | Mapa funcional dos contextos do projeto — índice, dependências e nível de confiança (`functional-map.md`) |
| `.agents/skills/` | Skills carregadas sob demanda: **de domínio** (motor de xadrez, partida em tempo real, usuários, validação de sessão) e **técnicas** (persistência, autenticação, casos de uso e API, testes) |

O carregamento das skills é dirigido pelo `description` do frontmatter de cada uma — não existe
índice manual de skills e nenhum deve ser criado. As skills de autoria
(`skill-authoring`, `spec-authoring`, `plan-authoring`, `tasks-authoring`,
`task-execution-authoring`, `test-cases-authoring`) são **moldes**: use-as ao criar ou revisar
artefato, não como documentação de domínio.

Onde `.agents/skills/` se sobrepuser a uma skill genérica de `.claude/skills/`,
**`.agents/skills/` prevalece** — ela descreve este repositório; as de `.claude/skills/` são
bibliotecas de propósito geral.

## Fluxo de especificação — Spec Kit

Este repositório usa **Spec Kit** como fluxo único de especificação de feature. Não há segundo
fluxo concorrente.

| Artefato | Dono |
|---|---|
| `.specify/memory/constitution.md` | Spec Kit — fonte única de arquitetura |
| `.specify/templates/` | Spec Kit — molde de spec/plan/tasks/checklist |
| `specs/{NNN-feature}/` (`spec.md`, `plan.md`, `tasks.md`, `contracts/`) | Spec Kit |
| `.agents/context|maps|skills/` | Conhecimento do repositório, independente de feature |

Comandos disponíveis como skills do Claude Code: `speckit-constitution`, `speckit-specify`,
`speckit-clarify`, `speckit-plan`, `speckit-tasks`, `speckit-analyze`, `speckit-checklist`,
`speckit-implement`, `speckit-taskstoissues`, mais a extensão git (`speckit-git-feature`,
`speckit-git-commit`, `speckit-git-initialize`, `speckit-git-remote`, `speckit-git-validate`).
Os scripts são PowerShell (`.specify/scripts/powershell/`, `"script": "ps"` em
`.specify/init-options.json`) — este repositório é desenvolvido em Windows.

**Ponto de integração (não quebrar).** O Spec Kit está configurado com
`"context_file": "CLAUDE.md"` e o `speckit.plan` reescreve o ponteiro do plano ativo **entre os
marcadores `<!-- SPECKIT START -->` e `<!-- SPECKIT END -->` no `CLAUDE.md` da raiz**. Regras:

- não remover os marcadores nem o conteúdo entre eles, e não editá-lo à mão — ele é de máquina;
- não mover o `context_file` para `AGENTS.md`: este arquivo é estável e versionado por decisão
  humana, não por ferramenta;
- nenhuma convenção nova vai para o `CLAUDE.md` — tudo o que for instrução para agente entra aqui.

## Contexto por diretório

| Arquivo | O que cobre |
|---|---|
| [`.claude/CLAUDE.md`](.claude/CLAUDE.md) | Referência rápida da plataforma: stack, layout, convenções resumidas e índice de documentação |
| [`README.md`](README.md) | Visão geral, como rodar, contratos HTTP e SignalR, notação do tabuleiro |
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | Fluxos de request, máquina de estado do hub, internals da engine, log de decisões |
| [`docs/FRONTEND_CHANGES.md`](docs/FRONTEND_CHANGES.md) | Contrato consumido pelo front-end e histórico de mudanças |
| [`docs/debito-tecnico.md`](docs/debito-tecnico.md) | Débito conhecido, severidade e itens que exigem decisão humana |
