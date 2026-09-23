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

**Última revisão:** 2026-09-23, no **endurecimento de segurança pré-produção** — auditoria OWASP
Top 10 2021 / API Security Top 10 2023 / ASVS aplicada ao `Orchestrator`, com `dotnet test` em
760 aprovados / 0 ignorados. O relatório de controles, o checklist de produção e as limitações
que **permanecem** estão em [seguranca.md](./seguranca.md).

Saíram desta lista nesta rodada:

- **DT-07** — o access token deixou de ser gravado em claro na coleção `Validation`: o campo
  `AcessToken` passa a guardar o resumo SHA-256 (`UseCases/Security/TokenDigest.cs`), calculado e
  comparado dentro de `ValidationService`. O nome do campo foi mantido de propósito, para não
  exigir migração; registros antigos deixam de casar, o que custa uma reautenticação a quem
  estiver logado no momento do deploy.
- **DT-16** — `change-password` passou a derivar o usuário do claim `sub` e a ignorar `userId` e
  `modifiedBy` do corpo; `PUT /users/{id}` e `DELETE /users/{id}` passaram a receber o nível do
  chamador e a recusar tanto agir sobre quem está acima dele quanto conceder papel acima dele.
  `GET /users/{id}` ficou restrito à própria conta ou a nível ≥ `adm`, respondendo 404 na recusa.
- **DT-18** — `Infra/Mongo/MongoIndexInitializer.cs`, registrado como `IHostedService`, cria na
  subida o índice **único** de `User.Email` e os índices de `RefreshToken.UserId` e
  `Validation.UserId`, de forma idempotente. Falha na criação é `LogError` e não derruba a
  aplicação: o caso previsto é a coleção já ter e-mail duplicado, e ficar fora do ar por causa de
  um dado antigo seria pior que o problema.
- **DT-19** — `ChangePasswordUseCase` deixou de injetar `IGenericRepository`: o `$set` direcionado
  virou `IUserRepositoryNoSql.UpdatePassword`, implementado em `UserRepositoryNoSql`.

**Revisão anterior:** 2026-08-01, no refactor do motor (`refactor/motor-xadrez-modernizacao`),
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

Os quatro itens que moravam aqui saíram na revisão de 2026-09-23 — ver o cabeçalho. Os dois
abaixo **nasceram** nessa revisão: são o que a auditoria encontrou e a implementação deixou de
fora conscientemente, com o motivo registrado.

### DT-26 — access token não é revogável

Uma vez emitido, o JWT vale até expirar — no máximo 60 minutos. Isso vale **inclusive** depois de
troca de senha, de detecção de reuso de refresh token e de remoção do usuário: os três revogam os
refresh tokens, mas nenhum consegue cancelar um access token já em circulação. Quem tiver copiado
um token continua autenticado pelo resto da validade dele.

Ficou de fora de propósito: fechar isso exige uma lista de invalidação consultada a **cada**
requisição, o que troca uma verificação local de assinatura por uma ida ao banco em toda chamada
autenticada. É decisão de arquitetura com custo permanente, não um ajuste.

- **Arquivos**: `Orchestrator/UseCases/Security/TokenService.cs` (o claim `jti`, hoje emitido e
  não usado, é o gancho previsto), `Orchestrator/Composition/JwtComposition.cs`
- **Saída**: `[DECISÃO]` — ou aceitar a janela de 60 minutos e documentá-la como contrato, ou
  encurtar `Jwt:ExpiresMinutes` (o que aumenta a frequência de refresh), ou introduzir a lista de
  invalidação por `jti` com um cache em memória e pagar o custo. A coleção `Validation` **não**
  serve como blacklist: ela é autorização de sessão de jogo, não de token.

### DT-27 — o limite de requisições depende do IP visto pelo Kestrel

`RateLimitingComposition` particiona por `HttpContext.Connection.RemoteIpAddress`. Atrás de um
proxy reverso ou balanceador — que é como isto será publicado — esse endereço é o **do proxy**, e
não o do cliente: todos os usuários caem na mesma partição e o teto de 20 requisições por minuto
vira um teto global para a aplicação inteira, derrubando usuário legítimo.

Não foi resolvido aqui porque `ForwardedHeaders` exige saber a rede confiável do proxy, e
configurar `KnownProxies`/`KnownNetworks` com valor errado é pior que não configurar: passa a
aceitar o `X-Forwarded-For` que o cliente mandar, e aí qualquer um escolhe a própria partição e
escapa do limite.

Segundo efeito, menor: o estado do limitador vive em memória, por instância. Duas instâncias
multiplicam o teto por dois, e reiniciar zera as janelas.

- **Arquivos**: `Orchestrator/Program.cs`, `Orchestrator/Composition/RateLimitingComposition.cs`
- **Saída**: `[DECISÃO]` — ao definir a topologia de deploy, configurar `UseForwardedHeaders` com
  a rede do proxy **antes** de `UseRateLimiter`. Está no checklist de
  [seguranca.md](./seguranca.md), item 7.

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

### DT-24 — `MakeMove` em sala inexistente responde `Game not started.`

A mensagem manda o cliente investigar o estado da partida quando o problema é o nome da sala. Quem
depura pela mensagem procura no lugar errado. Descoberto ao capturar os payloads reais para
[`fluxo-req-res.md`](./fluxo-req-res.md).

- **Arquivo**: `Orchestrator/Infra/SignalR/ChessHub.cs` (`MakeMove`, `GetPossibleMoves`)
- **Saída**: distinguir "sala não encontrada" de "partida não iniciada", como `StartGame` já faz
  (`Room '{room}' not found.`). Mudança de mensagem é mudança de contrato: a suíte E2E afirma
  algumas dessas strings, então é o [cenário A](./workflow-cenarios.md) — as duas pontas juntas.

### DT-25 — `GetPlayersInRoom` devolve contagem, não jogadores

O nome promete uma lista e o método devolve um `int`. Pior: para uma sala inexistente devolve `0`,
indistinguível de sala vazia — o cliente não consegue diferenciar "não existe" de "está vazia".
`JoinRoom` e `PlayerJoined` já carregam a lista de jogadores, então hoje ninguém depende disto.

- **Arquivo**: `Orchestrator/Infra/SignalR/ChessHub.cs`
- **Saída**: ou renomear para `GetPlayerCount` e devolver `int?` (`null` para sala inexistente), ou
  passar a devolver a lista e ajustar o nome ao contrato. A primeira é menor e resolve a
  ambiguidade real.

### DT-17 — busca por Id via `Id.ToString() == id`

`GetUserUseCase`, `UpdateUserUseCase` e `DeleteUserUseCase` filtram com
`user => user.Id.ToString() == id`, delegando ao driver a tradução de `ToString()` sobre um `Guid`
serializado como string. (`ChangePasswordUseCase` saiu desta lista em 2026-09-23: ele passou a
usar `Guid.TryParse` + `GetById`, que é exatamente a saída descrita abaixo.) Funciona hoje, mas é frágil: depende do tradutor de expressão do
`MongoDB.Driver` e não usa índice de forma previsível. `BaseRepositoryNoSql.GetById` faz o certo
(`Guid.TryParse` + `x.Id == guid`).

- **Saída**: trocar por `Guid.TryParse` + comparação direta, ou usar `GetById` do repositório
  (que já existe e está sem uso nesses caminhos).

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
