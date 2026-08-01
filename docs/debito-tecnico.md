# Débito técnico — Hibrygame

Documento vivo. Registro central de desvio conhecido, com severidade e caminho de saída.
Item marcado `[DECISÃO]` exige definição humana antes de qualquer implementação.

**Regras deste arquivo**

- Item resolvido **sai** desta lista e é citado no PR que o resolveu.
- Ao encontrar um desvio novo, registre aqui **antes** de "corrigir de passagem" — corrigir sem
  registrar esconde o tamanho real da dívida.
- Não use este arquivo como lista de tarefas priorizada: prioridade é decisão do dono do repo.

**Levantamento inicial:** 2026-07-31, sobre `main` + working tree, com `dotnet test` em
453 aprovados / 1 ignorado.

**Última revisão:** 2026-08-01, no refactor do motor (`refactor/motor-xadrez-modernizacao`),
com `dotnet test` em 563 aprovados / 0 ignorados, e 71/71 no smoke de sistema. Relatório completo em
[refactor-2026-08-01.md](./refactor-2026-08-01.md).

Saíram desta lista naquela rodada: **DT-01** (engine sem pacotes ASP.NET Core), **DT-03**
(infraestrutura morta removida, junto com MediatR e Polly), **DT-05** (`ValidationService`
com `ILogger`, membros mortos e filtro permissivo removidos — sobrou só a decisão sobre
`email`/`day`, ver DT-20), **DT-10** (`EnumMember` inerte e errado removido), **DT-11**
(cavalo reescrito com oito deltas, sem mutar o tabuleiro; `Skip` removido), **DT-12**
(`Position` com igualdade de valor), **DT-14** (`CreateRoom.AlreadyExisted` via `TryAdd`) e
**DT-06** (`appsettings.json` renomeado para a seção `Mongo:*` que o código realmente lê),
**DT-04** (`POST /users` exige `Role:Admin` e deriva `CreatedBy` do token; auto-registro
via `POST /register`, que fixa o papel em `jogador` no servidor), **DT-13** (xeque-mate e
afogamento detectados, `GameRoom.Finish()` finalmente chamado), **DT-20** (`email`/`day`
fora da assinatura de `GetValidationCanMove`), **DT-21** (reentrada na sala após
reconectar) e **DT-23** (o servidor passou a honrar a cor pedida no lobby, e avisa quando
não pode).

Achados na mesma rodada, ao rodar o sistema de verdade contra MongoDB — todos corrigidos e
descritos em [manual-testing-results.md](./manual-testing-results.md):

- `Jwt:Key` tinha 240 bits e HS256 exige 256, então **todo login era impossível**, e a
  exceção real era engolida num `"Login failed"` genérico.
- `JwtBearerOptions.MapInboundClaims` vinha `true`, renomeando `sub` para
  `ClaimTypes.NameIdentifier`. `User.FindFirst(JwtRegisteredClaimNames.Sub)` era sempre
  `null`, e por isso **os quatro endpoints de `/validation` respondiam 403 para todo
  usuário, sempre**.

## Severidade alta — segurança

### DT-07 — access token gravado em claro na coleção `Validation`

`LoginAsyncUseCase` chama `ValidationService.CreateValidation` gravando o JWT completo em
`Validation.AcessToken` (sic) e as consultas comparam o token em claro
(`x.AcessToken == accessToken`). Vazamento da coleção entrega sessões ativas prontas para uso.
Contraste: o refresh token é hasheado com PBKDF2 — o access token, não.

- **Arquivos**: `Orchestrator/UseCases/LoginAsyncUseCase.cs`,
  `Orchestrator/UseCases/ValidationService.cs`, `Orchestrator/Domain/Validation.cs`
- **Saída**: guardar hash do token (ou o `jti` do JWT, que já existe nos claims) em vez do token
  inteiro; ajustar os quatro métodos de consulta. Renomear `AcessToken` → `AccessToken` no mesmo
  PR (quebra de schema: ver DT-08).

### DT-16 — `change-password` e `PUT /users` não checam quem é o solicitante

Dois endpoints autenticados agem sobre um usuário identificado **pelo corpo do request**, sem
comparar com o claim `sub`:

- `POST /users/change-password` (`Role:Player`) — `ChangePasswordRequest.UserId` vem do corpo.
  Qualquer jogador autenticado troca a senha de qualquer usuário, desde que saiba a senha atual
  daquele usuário. `ModifiedBy` também vem do corpo.
- `PUT /users/{id}` (`Role:TeamLeader`, nível 3) — permite gravar `Role = "super adm"` (nível 5).
  Escalonamento de privilégio para cima do próprio nível.

O padrão correto já existe no repositório: `ValidationController.IsCallerAuthorizedFor` compara
`sub` com o id do corpo e devolve `Forbid()`.

- **Arquivos**: `Orchestrator/Presentation/UserController.cs`,
  `Orchestrator/UseCases/ChangePasswordUseCase.cs`, `Orchestrator/UseCases/UpdateUserUseCase.cs`
- **Saída**: derivar o id-alvo do claim `sub` em `change-password` (ou exigir `Role:Admin` para
  trocar a senha de outro); em `PUT /users/{id}`, recusar papel-alvo maior que o papel do
  solicitante. `ModifiedBy`/`CreatedBy` sempre do claim, nunca do corpo.

### DT-18 — `User.Email` sem índice único

A unicidade de e-mail é garantida em código (`FindByFilter` + `if`) em `CreateUserUseCase` e
`UpdateUserUseCase`, sem índice único no Mongo. Duas criações simultâneas com o mesmo e-mail
passam as duas, e o login (`FirstOrDefault` por e-mail) passa a depender de ordem de retorno.
Nenhum índice é criado no startup — `User.Email` e `RefreshToken.UserId` são varredura de coleção.

- **Saída**: criar índice único em `User.Email` (normalizado) e índice em `RefreshToken.UserId`,
  de forma idempotente no startup. Decidir onde esse código vive — não existe ponto de
  inicialização de banco hoje.

## Severidade média — arquitetura

### DT-02 — `Domain` depende de `Infra`

`User`, `RefreshToken` e `Validation` importam `Orchestrator.Infra.Utils` (por
`CollectionNameAttribute`) e `Orchestrator.Infra.Mongo`. Isso inverte o fluxo
`Domain → UseCases → Infra` do Princípio I. Como tudo está no mesmo projeto, o compilador não
reclama — a fronteira é só convenção.

- **Arquivos**: `Orchestrator/Domain/*.cs`, `Orchestrator/Infra/Utils/CollectionNameAtribute.cs`
- **Saída**: mover `CollectionNameAttribute` para `Domain/` (é metadado de domínio, não de
  infraestrutura) e remover os `using` de `Infra.Mongo` que não são usados. O arquivo ainda está
  grafado `CollectionNameAtribute.cs` (um "t"): renomear no mesmo PR.

### DT-09 — sem escala horizontal: estado do hub em processo

`ChessHub` mantém `static ConcurrentDictionary<string, GameRoom>`. Duas instâncias da API não
compartilham sala: jogadores conectados em instâncias diferentes não se veem. Também não há
persistência de partida — restart do processo perde tudo.

- **Arquivo**: `Orchestrator/Infra/SignalR/ChessHub.cs`
- **Saída**: aceitável enquanto o deploy for instância única (é o caso hoje). Escalar exige
  backplane (Redis) **e** mover o estado de sala para fora do processo. Decisão adiada
  conscientemente — ver "Why static hub state" em `docs/ARCHITECTURE.md`.

## Severidade baixa — corretude e polimento

### DT-22 — regra `react-hooks/set-state-in-effect` desligada no frontend

Regra do `eslint-plugin-react-hooks 7`, voltada ao React Compiler. Acusa três sítios que são o
padrão "carregar na montagem" — `useChessGame` (`void start()`), `useChessLobby`
(`void loadRooms()`) e `useAuth` (ressincroniza token quando o `userId` da rota muda) — onde o
`setState` acontece depois de um `await`. Desligada no refactor de 2026-08-01, com a razão escrita
no próprio `eslint.config.js`. Os erros de `react-hooks/refs` do mesmo lote **eram** bugs reais e
foram corrigidos.

- **Arquivo**: `KrockSide/eslint.config.js`
- **Saída**: reescrever os três com `useSyncExternalStore` (ou equivalente) e religar a regra.
  É refactor da camada de estado do front, não conserto pontual.

### DT-17 — busca por Id via `Id.ToString() == id`

`GetUserUseCase`, `UpdateUserUseCase`, `DeleteUserUseCase` e `ChangePasswordUseCase` filtram com
`user => user.Id.ToString() == id`, delegando ao driver a tradução de `ToString()` sobre um `Guid`
serializado como string. Funciona hoje, mas é frágil: depende do tradutor de expressão do
`MongoDB.Driver` e não usa índice de forma previsível. `BaseRepositoryNoSql.GetById` faz o certo
(`Guid.TryParse` + `x.Id == guid`).

- **Saída**: trocar por `Guid.TryParse` + comparação direta, ou usar `GetById` do repositório
  (que já existe e está sem uso nesses caminhos).

### DT-19 — `ChangePasswordUseCase` injeta `IGenericRepository` direto

Todos os outros casos de uso dependem de `I{Entidade}RepositoryNoSql`; este injeta
`IGenericRepository` e monta a query genérica na mão. Fura a fronteira do Princípio I
(caso de uso conhecendo a camada genérica de persistência) e dificulta o mock no teste.

O `Update` direcionado com `$set` que ele faz **está correto** e é melhor que `ReplaceOne` — a
saída é expor esse update no `IUserRepositoryNoSql`, não copiar a injeção genérica.

- **Arquivo**: `Orchestrator/UseCases/ChangePasswordUseCase.cs`
- **Saída**: adicionar um método específico (ex.: `UpdatePassword`) em `IUserRepositoryNoSql` /
  `UserRepositoryNoSql` e injetar a interface da entidade.

### DT-15 — sem linter, formatter e cobertura no gate

Não há `.editorconfig`, `dotnet format` no CI nem relatório de cobertura, embora
`coverlet.collector` esteja instalado nos dois projetos de teste. Estilo é mantido por
convenção manual; formatação inconsistente já aparece em `Move.cs` e `Pawn.cs`.

- **Saída**: `[DECISÃO]` — adicionar `.editorconfig` + `dotnet format --verify-no-changes` no
  workflow implica um PR grande de reformatação de uma vez. Vale decidir se o custo compensa
  antes de fazer.

## Artefatos de build versionados

`bin/`, `obj/`, `*.dll` e `*.pdb` estão no `.gitignore`, mas parte deles **já estava no índice do
git** antes do ignore existir, então continuam aparecendo como modificados em todo `git status` e
poluem todo diff.

- **Saída**: `git rm -r --cached` nos caminhos afetados, em um commit isolado que só remove
  rastreamento (nenhum arquivo é apagado do disco). Não misturar com mudança de código.
