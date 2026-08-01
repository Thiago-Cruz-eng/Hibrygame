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

## Severidade alta — segurança

### DT-04 — `POST /users` é anônimo e aceita o papel pelo corpo do request

`UserController.CreateUser` é `[AllowAnonymous]` e `CreateUserRequest.Role` vai direto para
`RoleHierarchy.TryGetLevel`. Qualquer pessoa na internet cria um usuário `"super adm"` e passa a
ter acesso a todos os endpoints, inclusive `DELETE /users/{id}`. `CreatedBy` também vem do corpo,
então a auditoria de criação é forjável.

- **Arquivos**: `Orchestrator/Presentation/UserController.cs`,
  `Orchestrator/UseCases/Dto/Request/CreateUserRequest.cs`,
  `Orchestrator/UseCases/CreateUserUseCase.cs`
- **Saída**: `[DECISÃO]` — escolher o modelo de cadastro: (a) auto-registro anônimo fixado em
  `"jogador"`, ignorando `Role` do request; (b) criação só por `Role:Admin`, com `CreatedBy`
  derivado do claim `sub`; (c) auto-registro anônimo + endpoint separado autenticado para
  elevar papel. Em qualquer opção, `CreatedBy` deve sair do token, não do corpo.

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

### DT-05 — `ValidationService` engole exceção, tem método não implementado e filtro permissivo

Três problemas na mesma classe:

1. Todo método é `try { ... } catch (Exception e) { return false; }` — falha de rede, filtro
   errado e "não encontrado" são indistinguíveis, e `e` nunca é logado (não há `ILogger`
   injetado, contrariando o Princípio III da constituição).
2. `GetValidationByUserIdTokenAndRoom` lança `NotImplementedException` e está na interface
   `IValidationService`.
3. A sobrecarga `GetValidationCanMove(userId, room, accessToken)` filtra com
   `x.AcessToken == accessToken || (x.UserId == userId && x.Room == room)` — o `||` aceita
   qualquer registro que casse **só** pelo token, sem amarrar o usuário. A outra sobrecarga
   (a usada pelo controller) tem `&&` e está correta, e ainda carrega dois filtros comentados
   (`UserEmail`, `DayOfGame`) cujo parâmetro é recebido e ignorado.

- **Arquivo**: `Orchestrator/UseCases/ValidationService.cs`
- **Saída**: injetar `ILogger<ValidationService>`, trocar `catch → return false` por log +
  retorno de falha explícito, remover o método não implementado da interface, trocar `||` por
  `&&` e decidir se `UserEmail`/`DayOfGame` entram no filtro ou saem da assinatura.

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

### DT-01 — engine referencia pacotes de ASP.NET Core sem usar

`Hibrygame.csproj` referencia `Microsoft.AspNetCore.Mvc.Core` 2.2.5 e
`Microsoft.AspNetCore.Hosting.Abstractions` 2.2.0. Nenhum arquivo da engine usa qualquer tipo
desses pacotes, e as versões são da era .NET Core 2.2. Viola o Princípio I (engine sem
framework) e ainda arrasta dependências antigas para quem consome a biblioteca.

- **Arquivo**: `Hibrygame/Hibrygame.csproj`
- **Saída**: remover os dois `PackageReference` e rodar `dotnet build` + `dotnet test`.
  Baixo risco — é o item mais barato desta lista.

### DT-02 — `Domain` depende de `Infra`

`User`, `RefreshToken` e `Validation` importam `Orchestrator.Infra.Utils` (por
`CollectionNameAttribute`) e `Orchestrator.Infra.Mongo`. Isso inverte o fluxo
`Domain → UseCases → Infra` do Princípio I. Como tudo está no mesmo projeto, o compilador não
reclama — a fronteira é só convenção.

- **Arquivos**: `Orchestrator/Domain/*.cs`, `Orchestrator/Infra/Utils/CollectionNameAtribute.cs`
- **Saída**: mover `CollectionNameAttribute` para `Domain/` (é metadado de domínio, não de
  infraestrutura) e remover os `using` de `Infra.Mongo` que não são usados. O arquivo ainda está
  grafado `CollectionNameAtribute.cs` (um "t"): renomear no mesmo PR.

### DT-03 — infraestrutura morta herdada de outro projeto

Existe e não é usado por nada:

- `ServiceCollectionExtensions.AddShared()` — registra MediatR, `IServiceFactory` e
  `ServiceInstanceResolver<>`; **nunca é chamado** no `Program.cs`.
- `ServiceFactory` / `IServiceFactory` / `ServiceInstanceResolver<>` — padrão de fábrica por país
  copiado do `verum-sales-global-backend`; aqui não há multi-país. `ServiceFactory` ainda usa
  `Console.WriteLine` para erro.
- `MongoDbContextFactory.CreateAsync(string country)` — parâmetro `country` que ninguém passa,
  connection string e nome de banco hardcoded, e o `Program.cs` registra o serviço mas nenhum
  consumidor o resolve.
- `MediatR` 14 e `Polly` 8 no `.csproj` sem uso.

Custo real: um agente ou pessoa nova lê esse código e conclui que existe pipeline de mediator ou
resolução por país, e escreve código novo em cima de algo que não roda.

- **Saída**: `[DECISÃO]` — remover tudo (recomendado, é a leitura honesta do projeto de hoje) ou
  declarar por escrito que é preparação para multi-instância e passar a usar. Meio-caminho não
  serve.

### DT-06 — chave de configuração do Mongo divergente

`Program.cs` lê `Mongo:ConnectionString` e `Mongo:Database`; `appsettings.json` declara
`HibrygameDatabase:{ConnectionString,DatabaseName,HibrygameCollectionName}`. As chaves nunca
casam, então o app **sempre** usa o fallback (`mongodb://localhost:27017`, base `Hibrygame`) e o
`appsettings.json` é decorativo. Trocar o Mongo pelo arquivo de configuração hoje não tem efeito.

- **Arquivos**: `Orchestrator/Program.cs`, `Orchestrator/appsettings.json`
- **Saída**: escolher uma das duas nomenclaturas e alinhar `README.md`,
  `docs/ARCHITECTURE.md` e `.claude/CLAUDE.md` no mesmo PR (os três documentam `Mongo:*`, que é
  o lado que o código lê).

### DT-09 — sem escala horizontal: estado do hub em processo

`ChessHub` mantém `static ConcurrentDictionary<string, GameRoom>`. Duas instâncias da API não
compartilham sala: jogadores conectados em instâncias diferentes não se veem. Também não há
persistência de partida — restart do processo perde tudo.

- **Arquivo**: `Orchestrator/Infra/SignalR/ChessHub.cs`
- **Saída**: aceitável enquanto o deploy for instância única (é o caso hoje). Escalar exige
  backplane (Redis) **e** mover o estado de sala para fora do processo. Decisão adiada
  conscientemente — ver "Why static hub state" em `docs/ARCHITECTURE.md`.

## Severidade baixa — corretude e polimento

### DT-10 — `ColorEnum` com `EnumMember` trocado

```csharp
[EnumMember(Value = "black")]  Black,
[EnumMember(Value = "bhite")]  White,   // typo
[EnumMember(Value = "white")]  None     // valor de White aplicado a None
```

`White` serializa como `"bhite"` e `None` como `"white"`. Só afeta quem serializa o enum com
`Newtonsoft.Json` — o hub converte com `.ToString()`, então o contrato do `/chesshub` está
correto hoje. É uma armadilha esperando o primeiro consumidor do conversor.

- **Arquivo**: `Hibrygame/Logic/Enums/ColorEnum.cs`
- **Saída**: `White → "white"`, `None → "none"`. Verificar antes se algum teste ou cliente já
  depende do valor errado.

### DT-11 — caso de borda do cavalo e teste ignorado

`KnightTests.GetMovesKnight_AfterOneMove_Correctly` está `Skip`. A aritmética do teste está
errada, **e** a engine ainda tem casos de borda no cavalo: `Move.CalculatePossibleMove` trata o
cavalo por desvio dentro do laço de direções, criando um `Knight` temporário
(`newPosition.Piece = new Knight(...)`) e mutando o tabuleiro durante o cálculo.

- **Arquivos**: `Hibrygame/Logic/Move.cs`, `Hibrygame.Test/Hibrygame/KnightTests.cs`
- **Saída**: extrair o cavalo para cálculo próprio (oito deltas explícitos), sem mutar o
  tabuleiro; reescrever o teste com a aritmética correta e remover o `Skip`.

### DT-12 — `Position` sem igualdade de valor

`Position` não sobrescreve `Equals`/`GetHashCode`. Quem precisa comparar por coordenada usa
`Common.PositionComparer` — mas só `Move.MakeMove` usa. `VerifyKingMovementationCheck` e
`PossiblePiecesHelpersToKingCheck` usam `List.Contains`/`Remove` sem comparador, ou seja,
igualdade por referência. Funciona por acidente enquanto todas as posições vierem do mesmo array
`Board.Positions`, e quebra silenciosamente quando alguém cria uma `Position` nova (como
`Position.TryFromAlgebraic` faz).

- **Arquivos**: `Hibrygame/Logic/Position.cs`, `Hibrygame/Logic/Move.cs`
- **Saída**: implementar `Equals`/`GetHashCode` por `(Row, Column)` em `Position`, ou passar
  `PositionComparer` explicitamente em todos os `Contains`/`Remove`. A primeira opção elimina a
  classe inteira de bug; exige rodar a suíte da engine com atenção.

### DT-13 — fim de partida nunca acontece

`GameRoom.Finish()` existe, `Finished` é checado em `MakeMove` e em `GetAvailableRooms`, mas nada
chama `Finish()`. Não há xeque-mate, empate, desistência nem relógio: uma partida só termina se
os dois jogadores desconectarem. Também não há promoção de peão, roque nem en passant.

- **Arquivos**: `Orchestrator/Infra/SignalR/GameRoom.cs`, `Hibrygame/Logic/Move.cs`
- **Saída**: escopo de feature, não bug. Xeque-mate depende de "nenhum movimento legal para a
  cor da vez", que depende de DT-11/DT-12 estarem resolvidos para ser confiável.

### DT-14 — `CreateRoom.AlreadyExisted` sempre reporta `false` para sala nova e vazia

```csharp
var created = Rooms.GetOrAdd(room, name => new GameRoom(name));
AlreadyExisted = created != Rooms[room] || created.Players.Count > 0
```

`created != Rooms[room]` é sempre `false` (é o mesmo objeto), então o campo na prática significa
"a sala tem jogador", não "a sala já existia". Recriar uma sala existente e vazia devolve
`AlreadyExisted = false`.

- **Arquivo**: `Orchestrator/Infra/SignalR/ChessHub.cs`
- **Saída**: usar `TryAdd` e derivar `AlreadyExisted` do retorno, ou renomear o campo para
  `HasPlayers` — mudança de contrato de FE, registrar em `docs/FRONTEND_CHANGES.md`.

### DT-08 — grafias erradas no contrato público

- `Validation.AcessToken` → `AccessToken` (falta o segundo "c") — está no schema do Mongo e no
  DTO `ValidationDto`.
- `Orchestrator/Infra/Utils/CollectionNameAtribute.cs` → `CollectionNameAttribute.cs`.
- `Board.GetPositionsPlacedInBoard()` é apenas um alias de `GetPositionsPlaced()`.

- **Saída**: agrupar num único PR de renomeação. `AcessToken` está persistido: documentos
  antigos precisam de `[BsonElement("AcessToken")]` no campo renomeado ou de um passo de
  correção pontual (não há migration neste projeto — ver Princípio "sem migration").

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
