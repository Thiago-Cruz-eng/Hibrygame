---
name: functional-map
description: Mapa dos 4 contextos de negócio do Hibrygame (engine de xadrez em C# puro + API ASP.NET Core 8 com SignalR, MongoDB e JWT), com objetivo, evidência no código, dependências entre contextos, dependências técnicas e nível de confiança. Levantado em 2026-07-31 sobre a main mais working tree, com dotnet test em 453 aprovados e 1 ignorado.
metadata:
  responsibility: "Mapa de identificação dos domínios de negócio, suas fronteiras e dependências. Índice de domínios para geração de skill e para o fluxo spec-driven; não detalha regra de negócio nem duplica as decisões transversais, que vivem no discovery-answers.md."
---

# Mapa funcional — Hibrygame

Plataforma de xadrez multiplayer. O recorte abaixo é por **contexto de negócio**, não pela
estrutura de pastas: a organização física é por camada (`Domain`, `UseCases`, `Infra`,
`Presentation`), e o mesmo contexto costuma aparecer em três delas.

O projeto é pequeno o bastante para caber em quatro contextos — e é honesto dizer que dois deles
(**Motor de Xadrez** e **Partida em Tempo Real**) concentram praticamente todo o valor, enquanto
os outros dois (**Identidade e Acesso**, **Sessão de Jogo**) são infraestrutura de produto herdada
de um scaffold mais ambicioso do que o jogo precisa hoje.

As decisões transversais (persistência, serialização, testes, segurança, contrato de FE) **não** se
repetem aqui — vivem no [`discovery-answers.md`](../context/discovery-answers.md) e aparecem por
contexto apenas quando afetam aquele recorte.

**O que fica fora dos quatro blocos, e por quê.**

- **Persistência, autenticação/autorização, casos de uso/API e testes** são convenções técnicas
  transversais, não contextos de negócio: vivem nas skills `persistencia-mongodb`,
  `autenticacao-e-autorizacao`, `casos-de-uso-e-api-http` e `estrategia-de-testes`.
- **Código morto herdado** — `IServiceFactory`/`ServiceFactory`/`ServiceInstanceResolver<>`,
  `ServiceCollectionExtensions.AddShared()`, `MongoDbContextFactory.CreateAsync(string country)`,
  e os pacotes `MediatR`/`Polly`. Nada disso é usado (DT-03). Não mapear; remover.

---

## 1. Motor de Xadrez

**Objetivo.** Representar tabuleiro, casas e peças, calcular movimentos legais por peça, detectar
xeque e aplicar jogada com rollback quando ela expõe o próprio rei. É a única regra de negócio
verdadeiramente complexa do repositório.

**Evidência no código.**
`Hibrygame/Logic/`: `Board.cs` (montagem e acesso), `Position.cs` (coordenada dupla + notação
algébrica), `Piece.cs` (abstrata) e as seis peças (`Pawn`, `Knight`, `Bishop`, `Rook`, `Queen`,
`King`), `Move.cs` (`CalculatePossibleMove`, `MakeMove`, `IsKingInCheck` — ~260 linhas, o coração),
`Common.cs` (limites, validade, oponentes, `PositionComparer`), `Enums/`.
Testes: `Hibrygame.Test/Hibrygame/` — 9 arquivos, 73 aprovados, 1 ignorado.

**Depende de.** Nada. É biblioteca pura, sem referência a framework, banco ou transporte
(Princípio I). É consumida por *Partida em Tempo Real*.

**Dependências técnicas.** `Newtonsoft.Json` apenas nos conversores de enum (`EnumStringConverter`).
Os dois `PackageReference` de ASP.NET Core no `.csproj` são resíduo não usado (DT-01).

**Escopo real (não presuma xadrez completo).** Sem promoção de peão, roque, en passant,
xeque-mate, empate, desistência, relógio, histórico de jogadas e notação PGN/FEN. `HasAlreadyOneMove`
existe só para o avanço duplo do peão.

**Confiança: alta** para a estrutura e o contrato; **média** para a corretude das regras — há
casos de borda conhecidos no cavalo (DT-11), igualdade de `Position` por referência em dois
caminhos (DT-12) e a detecção de xeque depende de efeito colateral dentro do cálculo de movimento.

**Skill:** `motor-de-xadrez`.

---

## 2. Partida em Tempo Real

**Objetivo.** Hospedar partidas: criar sala, admitir dois jogadores e atribuir cor, iniciar o jogo,
autorizar e aplicar jogadas, alternar turno e propagar o estado do tabuleiro para os participantes.
É onde vive a **autoridade do servidor** (Princípio II).

**Evidência no código.**
`Orchestrator/Infra/SignalR/ChessHub.cs` (10 métodos invocáveis, 6 eventos, 7 DTOs aninhados),
`GameRoom.cs` (`Board`, `Players`, `CurrentTurn`, `Started`, `Finished`, `TryAssignColor`,
`SwitchTurn`), registro em `Program.cs` (`app.MapHub<ChessHub>("/chesshub")`).
Testes: `Orchestrator.Test/ChessHubTests.cs` (~27 KB) e `GameRoomTests.cs`.

**Depende de.** *Motor de Xadrez* (cálculo e aplicação de jogada) e *Identidade e Acesso* (o hub
exige `Role:Player`; o JWT chega por query string). **Não** depende de *Sessão de Jogo* — o hub
nunca consulta `IValidationService`.

**Dependências técnicas.** SignalR; estado `static ConcurrentDictionary<string, GameRoom>` em
processo, sem persistência e sem backplane (DT-09).

**Fronteira importante.** O hub é a borda entre notação algébrica (cliente) e índice `Row`/`Column`
(engine), e é o único lugar autorizado a fazer essa conversão no fluxo de jogo.

**Confiança: alta.** As seis checagens de `MakeMove` estão implementadas e cobertas por teste. Os
pontos fracos são de escopo, não de corretude: partida nunca termina (`Finish()` nunca é chamado,
DT-13), reconexão não recupera a cor (o slot é do `ConnectionId`), salas vazias nunca são
removidas, e `CreateRoom.AlreadyExisted` não significa o que o nome diz (DT-14).

**Skill:** `partida-em-tempo-real-signalr`.

---

## 3. Identidade e Acesso

**Objetivo.** Cadastrar usuário, autenticar, emitir e rotacionar credenciais, e modelar quem o
usuário é: papel primário (que autoriza) e `Assignments` de time/hierarquia (metadado
organizacional que hoje não autoriza nada).

**Evidência no código.**
`Orchestrator/Domain/`: `User.cs`, `UserAssignment.cs` (+ `record HierarchyNode`),
`RefreshToken.cs`, `AuditInformation.cs`, `BaseEntity.cs`.
`Orchestrator/UseCases/`: `CreateUserUseCase`, `GetUserUseCase`, `UpdateUserUseCase`,
`DeleteUserUseCase`, `ChangePasswordUseCase`, `LoginAsyncUseCase`, `RefreshTokenUseCase`;
`Security/TokenService`, `Security/SecureHashingService`,
`Security/Authorization/{RoleHierarchy,MinimumRoleRequirement,MinimumRoleHandler}`.
`Orchestrator/Presentation/UserController.cs` (7 endpoints).
`Program.cs`: `AddJwtBearer` + as 5 policies `Role:*`.
Testes: `Orchestrator.Test/Domain/`, `UseCases/`, `Security/`.

**Depende de.** Nada de negócio. É consumido por *Partida em Tempo Real* (policy do hub) e por
*Sessão de Jogo* (o login cria a validação).

**Dependências técnicas.** MongoDB (coleções `User`, `RefreshToken`), JWT HS256, PBKDF2.

**Confiança: alta** para o modelo de token (dois tokens, rotação, hash, cadeia de revogação — é a
parte melhor construída do Orchestrator); **baixa** para a superfície de autorização dos
endpoints. Três problemas reais: `POST /users` é anônimo e aceita o papel pelo corpo (DT-04),
`change-password` e `PUT /users/{id}` não comparam o solicitante com o alvo (DT-16), e não há
índice único em `User.Email` (DT-18).

**Skill:** `usuarios-e-atribuicoes` (domínio) + `autenticacao-e-autorizacao` (técnica).

---

## 4. Sessão de Jogo (Validation)

**Objetivo declarado.** Amarrar uma sessão autenticada a uma partida: qual token, qual usuário, em
qual sala, jogando de qual cor, em que dia — permitindo confirmar por REST "este token pode mover
esta cor nesta sala".

**Evidência no código.**
`Orchestrator/Domain/Validation.cs`, `UseCases/ValidationService.cs` (+ `ValidationDto`),
`UseCases/Interfaces/IValidationService.cs`, `Presentation/ValidationController.cs` (4 endpoints
`POST`), `Dto/Request/ValidationRequests.cs`, `Dto/Response/ValidationResponses.cs`.
Testes: `Orchestrator.Test/UseCases/ValidationServiceTests.cs`,
`Presentation/ValidationControllerTests.cs` (~20 KB — o controller é bem coberto).

**Depende de.** *Identidade e Acesso* (o registro nasce no `LoginAsyncUseCase`).
**Ninguém depende dele** no fluxo de jogo: o `ChessHub` não o consulta.

**Confiança: alta** sobre o que o código faz; **o valor do contexto é que está em questão.**
Ele duplica, de forma mais frágil, informação que o `GameRoom` já tem em memória (quem está em qual
sala com qual cor) e que o JWT já carrega (quem é o usuário). É o subdomínio com mais defeito por
linha do repositório: token gravado em claro (DT-07), exceção engolida sem logger, método na
interface lançando `NotImplementedException`, filtro com `||` que aceita token de outro usuário
(DT-05), parâmetros recebidos e ignorados, e `{id}` de rota não usado.

**Gap aberto — `[DECISÃO]`:** manter, redesenhar ou remover este contexto. A skill
`validacao-de-sessao-de-jogo` descreve os três caminhos coerentes. Nenhuma feature nova deve
ampliar esta superfície antes da decisão.

**Skill:** `validacao-de-sessao-de-jogo`.

---

## Dependências entre contextos

```
Motor de Xadrez  ←──  Partida em Tempo Real  ──→  Identidade e Acesso
   (puro)                    (hub)                     (JWT/policy)
                                                             │
                                                             ▼
                                                    Sessão de Jogo
                                                 (criada no login,
                                                  não consumida pelo hub)
```

Nenhum ciclo. A única dependência que "falta" é intencional/duvidosa: o hub não usa a Sessão de
Jogo — ver o gap do contexto 4.

## Ordem sugerida ao evoluir o produto

1. **Motor de Xadrez** — resolver DT-11 (cavalo) e DT-12 (igualdade de `Position`) antes de
   qualquer feature de fim de partida: xeque-mate depende de "nenhum movimento legal", que depende
   desses dois estarem corretos.
2. **Identidade e Acesso** — fechar DT-04 e DT-16. Enquanto `POST /users` for anônimo com papel
   livre, toda a autorização do resto do sistema é decorativa.
3. **Partida em Tempo Real** — fim de partida (xeque-mate/empate/desistência), reconexão por
   identidade em vez de `ConnectionId`, limpeza de sala vazia.
4. **Sessão de Jogo** — decidir o destino do contexto (`[DECISÃO]`) antes de investir nele.
