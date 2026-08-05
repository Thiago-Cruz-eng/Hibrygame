# Implementation Plan: [FEATURE]

**Branch**: `[###-feature-name]` | **Date**: [DATE] | **Spec**: [link]
**Input**: Feature specification from `/specs/[###-feature-name]/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

[Extract from feature spec: primary requirement + technical approach from research]

## Technical Context

<!--
  ACTION REQUIRED: Replace the content in this section with the technical details
  for the project. The structure here is presented in advisory capacity to guide
  the iteration process.
-->

**Language/Version**: [e.g., Python 3.11, Swift 5.9, Rust 1.75 or NEEDS CLARIFICATION]  
**Primary Dependencies**: [e.g., FastAPI, UIKit, LLVM or NEEDS CLARIFICATION]  
**Storage**: [if applicable, e.g., PostgreSQL, CoreData, files or N/A]  
**Testing**: [e.g., pytest, XCTest, cargo test or NEEDS CLARIFICATION]  
**Target Platform**: [e.g., Linux server, iOS 15+, WASM or NEEDS CLARIFICATION]
**Project Type**: [e.g., library/cli/web-service/mobile-app/compiler/desktop-app or NEEDS CLARIFICATION]  
**Performance Goals**: [domain-specific, e.g., 1000 req/s, 10k lines/sec, 60 fps or NEEDS CLARIFICATION]  
**Constraints**: [domain-specific, e.g., <200ms p95, <100MB memory, offline-capable or NEEDS CLARIFICATION]  
**Scale/Scope**: [domain-specific, e.g., 10k users, 1M LOC, 50 screens or NEEDS CLARIFICATION]

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Princípio | Status | Observação |
|-----------|--------|-----------|
| I — Separação de camadas | ✅/❌ | `Hibrygame/` sem framework; `Domain → UseCases → Infra → Presentation` sem referência ascendente |
| II — Autoridade do servidor | ✅/❌ | Partida + identidade + turno + posse + legalidade recalculada + auto-xeque; nada confiado ao cliente |
| III — Use case por ação | ✅/❌ | `{Ação}{Entidade}UseCase` com try/catch + `ILogger` + `Response { Success, Message }`; controller fino |
| IV — Entidade protege estado | ✅/❌ | `BaseEntity`, setters protected, factory `Create`, mutador nomeado, auditoria, `[CollectionName]` |
| V — TDD + suíte verde | ✅/❌ | Teste antes do código; `dotnet test` ≥ 453 aprovados, 1 ignorado; sala de hub com nome único |
| VI — Contrato de FE | ✅/❌ | Método/evento/DTO do `/chesshub` e rotas HTTP preservados ou registrados em `docs/FRONTEND_CHANGES.md`; coordenadas algébricas |
| VII — Segredo fora do repo | ✅/❌ | Nenhum segredo em `appsettings.json`; token só em header (exceção `?access_token=` do `/chesshub`) |

> **Gate II — esclarecimento obrigatório quando a feature toca estado de jogo:**
> Se a feature adiciona ou altera método de hub que muda o tabuleiro, o plano DEVE listar
> explicitamente qual das seis checagens do Princípio II se aplica a cada caminho novo. Plano
> que não listar não passa o gate.

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)
<!--
  ACTION REQUIRED: Replace the placeholder tree below with the concrete layout
  for this feature. Delete unused options and expand the chosen structure with
  real paths (e.g., apps/admin, packages/something). The delivered plan must
  not include Option labels.
-->

```text
# Estrutura real deste repositório — liste apenas os caminhos que a feature toca.
Hibrygame/Logic/            # Engine: Board, Position, Piece (Pawn/Knight/Bishop/Rook/Queen/King), Move, Common, Enums/
Hibrygame.Test/Hibrygame/   # Testes da engine

Orchestrator/
├── Domain/                 # User, UserAssignment, RefreshToken, Validation, BaseEntity, AuditInformation
├── UseCases/               # Um caso de uso por ação
│   ├── Dto/{Request,Response}/
│   ├── Interfaces/         # ITokenService, ISecureHashingService, IValidationService
│   └── Security/           # TokenService, SecureHashingService, Authorization/
├── Infra/
│   ├── BaseRepository/     # IGenericRepository + GenericRepository
│   ├── Interfaces/         # I{Entidade}RepositoryNoSql
│   ├── Mongo/              # IMongoDbContext + factory
│   ├── Repositories/       # BaseRepositoryNoSql<T> + repos concretos
│   ├── SignalR/            # ChessHub + GameRoom
│   ├── Settings/           # JwtSettings
│   └── Utils/
├── Presentation/           # UserController, ValidationController
└── Program.cs              # Toda a composição de DI

Orchestrator.Test/          # Domain/, UseCases/, Security/, Presentation/, ChessHubTests, GameRoomTests
```

**Structure Decision**: [Document the selected structure and reference the real
directories captured above]

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
