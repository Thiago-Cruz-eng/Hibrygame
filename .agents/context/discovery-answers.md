---
name: discovery-answers
description: Contexto e decisões da descoberta do Hibrygame (backend, projeto pessoal retomado, escopo projeto inteiro) — objetivo, restrições herdadas do repositório, decisões transversais evidenciadas no código, formas de documentação mantidas e log de decisões pendentes. Levantado em 2026-07-31 junto com a portabilidade do harness vindo do verum-sales-global-backend.
metadata:
  responsibility: "Memória durável do contexto e das decisões de descoberta: objetivo, escopo, restrições declaradas, decisões transversais validadas (com destino), formas de documentação a manter e log de decisões humanas. Fonte relida pelas etapas seguintes; suas restrições precedem qualquer inferência posterior."
---

# Memória de descoberta — Hibrygame

## 1. Objetivo e escopo

**Objetivo.** Levantar o contexto canônico do Hibrygame para orientar as skills de domínio e o
fluxo spec-driven, no momento em que o repositório é retomado após pausa longa.

**Tipo de sistema:** backend — engine de xadrez em C# puro (`Hibrygame/`) + API REST/SignalR
ASP.NET Core 8 (`Orchestrator/`).
**Tipo de projeto:** projeto pessoal em **retomada**. O código existe, compila e tem suíte verde
(453 aprovados / 1 ignorado), mas não está em produção e não tem usuário real. Isso muda o cálculo
de risco: quebrar contrato é barato, e refatorar débito é mais valioso que preservar
compatibilidade.
**Escopo:** projeto inteiro (`Hibrygame/`, `Hibrygame.Test/`, `Orchestrator/`,
`Orchestrator.Test/`).

**Consequência para as etapas seguintes:** toda inferência é ancorada no código atual. Onde a
documentação divergir do código, o código prevalece — e a divergência é corrigida na documentação
no mesmo PR.

## 2. Restrições declaradas

O pedido que originou esta descoberta foi a **portabilidade do harness de agentes** do
`verum-sales-global-backend` para cá, sem restrição adicional declarada.

Ausência de restrição declarada não significa ausência de restrição: as restrições reais vêm do
repositório e estão consolidadas na seção 3. Elas têm o mesmo peso de uma restrição declarada e
**precedem qualquer inferência das etapas seguintes**.

### 2.1 Restrições herdadas do repositório (não negociáveis)

| Restrição | Origem | Efeito nas etapas seguintes |
|---|---|---|
| **Engine sem framework** — `Hibrygame/` não referencia `Orchestrator`, ASP.NET, Mongo nem SignalR | Constituição Princípio I (NON-NEGOTIABLE) | Regra de xadrez nova entra na engine, com teste na engine; nunca no hub |
| **Fluxo `Domain → UseCases → Infra → Presentation`** sem referência ascendente | Constituição Princípio I | Caso de uso depende só de interface; controller só de caso de uso |
| **Autoridade do servidor** — 6 checagens em `MakeMove` (partida, identidade, turno, posse, legalidade recalculada, auto-xeque) | Constituição Princípio II (NON-NEGOTIABLE) | Método de hub novo que altere o tabuleiro repete todas; lista de movimentos do cliente é sempre ignorada |
| **Use case por ação, sem exceção vazando** — try/catch + `ILogger<T>` + `Response { Success, Message }` | Constituição Princípio III | Controller nunca contém regra; exceção nunca chega nele |
| **Entidade protege o próprio estado** — `BaseEntity`, setters protected, factory `Create`, mutador nomeado, auditoria, `[CollectionName]` | Constituição Princípio IV | Modelo anêmico não é criado. `Validation` é o contraexemplo existente, não o padrão |
| **TDD obrigatório e suíte verde** — Red→Green→Refactor; baseline 453/1 | Constituição Princípio V | Toda task de feature começa pelo arquivo de teste |
| **Contrato de FE registrado** — hub, DTO e rota mudados exigem entrada em `docs/FRONTEND_CHANGES.md` | Constituição Princípio VI | Preferir adição a alteração; coordenada externa é sempre algébrica |
| **Segredo fora do repositório** — `Jwt:Key` do `appsettings.json` é valor de desenvolvimento; token nunca em URL de REST | Constituição Princípio VII | A exceção `?access_token=` vale só para `/chesshub` |
| **Sem migration** — MongoDB sem framework de migração e sem índice declarado | ausência no código | Plano não pressupõe migration; campo novo entra opcional / `[BsonIgnoreIfNull]` |
| **Sem transação** — atomicidade só por documento | `IGenericRepository` não expõe sessão | Fluxo que grava em duas coleções precisa ser idempotente ou tolerar o meio-caminho |

## 3. Decisões transversais evidenciadas no código

Decisões que já estão tomadas (o código as implementa) e cujo destino documental está definido.
Nenhuma delas se repete no `functional-map.md`.

| Tema | Decisão vigente | Onde vive |
|---|---|---|
| Persistência | `IGenericRepository` genérico + `BaseRepositoryNoSql<T>` + repo concreto por entidade; coleção por `[CollectionName]`; `Guid` e datas como **string** | skill `persistencia-mongodb` |
| Autenticação | JWT HS256 60 min + refresh 64B rotativo hasheado com PBKDF2, 30 dias, cadeia `ReplacedByTokenId` | skill `autenticacao-e-autorizacao` |
| Autorização | 5 policies `Role:{Nível}` sobre `MinimumRoleHandler`; papéis em português; `[Authorize(Roles=...)]` cru proibido | skill `autenticacao-e-autorizacao` |
| Camada de aplicação | um caso de uso por ação, `Response { Success, Message }`, DI manual no `Program.cs`, sem MediatR no pipeline | skill `casos-de-uso-e-api-http` |
| Real-time | hub único `/chesshub`, estado `static` em processo, snapshot completo de 64 casas | skill `partida-em-tempo-real-signalr` |
| Regra de xadrez | cálculo por direção + número de casas; xeque detectado por efeito colateral em `IsInCheckState` | skill `motor-de-xadrez` |
| Testes | xUnit + Moq, sem FluentAssertions, sem banco real, sem `WebApplicationFactory` | skill `estrategia-de-testes` |
| Serialização REST | `ReferenceHandler.Preserve` (`$id`/`$ref`/`$values`) — **não** afeta o SignalR | `docs/FRONTEND_CHANGES.md` |
| Fluxo de especificação | Spec Kit, scripts PowerShell, `context_file: CLAUDE.md` | `AGENTS.md` |

## 4. Formas de documentação mantidas

| Artefato | Papel | Regra |
|---|---|---|
| `AGENTS.md` | instruções canônicas para agentes | fonte única; `CLAUDE.md` e `.github/copilot-instructions.md` são ponteiros |
| `.specify/memory/constitution.md` | arquitetura não negociável | emenda exige bump de versão + propagação aos templates |
| `.agents/skills/` | verdade do domínio, carregada sob demanda | precede padrão inferido do código |
| `.agents/maps/functional-map.md` | fronteiras e dependências entre contextos | não detalha regra de negócio |
| `.agents/context/discovery-answers.md` | este arquivo | memória durável de contexto e decisão |
| `README.md` | visão geral, como rodar, contratos | público, para humano |
| `docs/ARCHITECTURE.md` | fluxos, máquina de estado, internals, log de decisões | detalhamento técnico |
| `docs/FRONTEND_CHANGES.md` | contrato de FE | append-only |
| `docs/debito-tecnico.md` | débito conhecido | item resolvido sai da lista |

## 5. Log de decisões

### 5.1 Decisão de 2026-07-31 — harness portado, não copiado

O harness veio do `verum-sales-global-backend`, que é multi-país, multi-tenant, com Clean
Architecture em 50 projetos e fluxo duplo (Spec Kit + Clovis). **Nada disso se aplica aqui.**
O que foi portado e o que foi deliberadamente deixado de fora:

**Portado como estrutura, reescrito como conteúdo:** constituição (7 princípios próprios, nenhum
herdado literalmente), `AGENTS.md`, `functional-map.md`, `discovery-answers.md`, skills de domínio
e técnicas, template de PR, workflow de CI.

**Portado verbatim (é agnóstico ao repositório):** `.specify/scripts/`, `.specify/templates/`
(exceto a tabela de Constitution Check do `plan-template.md`), `.specify/extensions/` (extensão
git), `.specify/workflows/`, e as seis skills de autoria em `.agents/skills/`
(`skill-authoring`, `spec-authoring`, `plan-authoring`, `tasks-authoring`,
`task-execution-authoring`, `test-cases-authoring`).

**Deliberadamente não portado:** o fluxo Clovis e a pasta `.clovis/` (estado de CLI, não versionável
como conteúdo de outro repo); `unified-pipeline.yml` (k8s + ACR + SonarQube + Trivy + ArgoCD —
não há k8s, registry nem Sonar aqui); as 38 skills de domínio do verum (vendas B2B, SAP, malha
fiscal, países) — inúteis neste repositório; `CODEOWNERS` com times da organização.

**Fluxo único:** este repositório usa **apenas** Spec Kit. Não replicar a fronteira
"Spec Kit × Clovis" do verum.

### 5.2 Decisões pendentes — `[DECISÃO]`

Exigem definição humana antes de implementação. Todas detalhadas em `docs/debito-tecnico.md`.

| # | Decisão | Bloqueia |
|---|---|---|
| D-01 | Modelo de cadastro de usuário: auto-registro anônimo fixado em `"jogador"`, criação só por `Role:Admin`, ou os dois com elevação separada (DT-04) | qualquer endurecimento de autorização; hoje toda a autorização é decorativa |
| D-02 | Destino do contexto *Sessão de Jogo*: remover, redesenhar com `jti`, ou rebaixar a log de sessão (DT-05/DT-07) | qualquer feature que toque `Validation` |
| D-03 | Remover a infraestrutura morta (`IServiceFactory`, `AddShared`, `MongoDbContextFactory`, MediatR, Polly) ou declarar por escrito para que serve e passar a usar (DT-03) | leitura correta do repositório por pessoa ou agente novo |
| D-04 | Adotar `.editorconfig` + `dotnet format` no CI, aceitando um PR grande de reformatação (DT-15) | consistência de estilo |
| D-05 | Qual chave de configuração do Mongo é a canônica: `Mongo:*` (o que o código lê) ou `HibrygameDatabase:*` (o que o `appsettings.json` declara) (DT-06) | tornar a configuração de banco efetiva |

Nenhuma dessas decisões precisa ser tomada para trabalhar no motor de xadrez ou na partida em tempo
real — que é onde está o valor do produto.
