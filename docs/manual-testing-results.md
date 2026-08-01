# Smoke de sistema — resultados

**Data:** 2026-08-01
**Branch:** `refactor/motor-xadrez-modernizacao` (nos dois repos)
**Resultado: 63/63 cenários de sistema · 608 testes automatizados · 0 avisos de build**

## Como isto foi executado

Não é uma suíte de testes com mocks. É um cliente de verdade batendo no sistema todo:

```
SmokeRunner (console C#)
   ├── HttpClient          → REST real       → MongoDB 8.2.12 (container `xadrez` no WSL)
   └── SignalR.Client      → WebSocket real  → ChessHub → motor de regras
        (JWT obtido do POST /login, não fabricado)
```

Dois clientes SignalR simultâneos, cada um autenticado com o token do seu usuário, jogando
uma partida um contra o outro. Sem stub em nenhuma camada.

Para rodar de novo:

```bash
# 1. Mongo de pé (no WSL)
docker run -d --name xadrez -p 27017:27017 -v xadrez-data:/data/db mongo:latest

# 2. API
dotnet run --project Orchestrator --no-launch-profile --urls "http://localhost:5199"

# 3. O runner fica no scratchpad da sessão (fora dos repos, por ser descartável):
#    scratchpad/SmokeRunner/  →  dotnet run
```

## O que este smoke encontrou

Duas coisas que **nenhum** dos 608 testes automatizados pegava, porque só aparecem quando
existe servidor e banco de verdade:

### 1. O frontend chamava três rotas REST que não existem

| `userApi` chamava | O backend expõe | Consequência |
|---|---|---|
| `POST create` | `POST /users` | Cadastro pela interface respondia **404** |
| `GET get/{id}` | `GET /users/{id}` | **Entrar em sala pela interface respondia 404** |
| `POST refresh` | `POST /refresh-token` | Renovação de token respondia 404 |

A do meio é a grave: `getUser` é a primeira chamada de `joinRoom`, então **era impossível
entrar numa sala pela UI**, mesmo com o tabuleiro já consertado.

Por que a suíte não pegou: os dublês reproduziam o mesmo erro do código. Os handlers do
MSW e o `axios-mock-adapter` respondiam em `create`, `get/:id` e `refresh` — as rotas
erradas. Teste e código concordavam entre si e discordavam do servidor. Corrigi os três
lados: `userApi`, os handlers do MSW e o teste.

### 2. `Jwt:Key` tinha 240 bits e HS256 exige 256

Já relatado antes, encontrado no mesmo esforço: a chave que vinha no `appsettings.json`
tornava **todo login impossível**, e o erro real (`IDX10720`) era engolido num
`"Login failed"` genérico com 401 — indistinguível de senha errada. Agora a aplicação
recusa subir com chave curta, dizendo o tamanho encontrado e o exigido.

## Resultados por área

### 1. REST — cadastro e autenticação (16/16)

| Cenário | Resultado |
|---|---|
| `POST /users` cria usuário A | ✅ HTTP 200 |
| `POST /users` cria usuário B | ✅ HTTP 200 |
| `POST /users` recusa e-mail duplicado | ✅ "User already has a account" |
| `POST /users` recusa confirmação de senha divergente | ✅ HTTP 400 |
| `POST /login` autentica A e emite JWT | ✅ token de 528 chars |
| `POST /login` autentica B e emite JWT | ✅ token de 527 chars |
| `POST /login` recusa senha errada | ✅ 401 "Invalid credentials" |
| `POST /login` recusa e-mail inexistente | ✅ HTTP 401 |
| `POST /refresh-token` rotaciona o refresh token | ✅ devolve token diferente |
| `POST /refresh-token` recusa token já usado | ✅ "Invalid refresh token" |
| `GET /users/{id}` devolve o usuário | ✅ e-mail confere |
| `GET /users/{id}` sem token é recusado | ✅ HTTP 401 |
| `POST /users/change-password` troca a senha | ✅ "Password updated" |
| `change-password` recusa confirmação divergente | ✅ HTTP 400 |
| A senha antiga deixa de funcionar | ✅ HTTP 401 |
| A senha nova funciona | ✅ HTTP 200 |

A rotação de refresh token funciona de verdade: o token antigo é invalidado no uso.

### 2. Autorização do hub (2/2)

| Cenário | Resultado |
|---|---|
| `/chesshub` sem token é recusado | ✅ HTTP 401 |
| `/chesshub` com token é aceito | ✅ HTTP 200 |

### 3. SignalR — conexão real (1/1)

| Cenário | Resultado |
|---|---|
| Dois clientes conectam por WebSocket com JWT | ✅ `Connected` / `Connected` |

Prova que o `accessTokenProvider` do cliente e o `OnMessageReceived` do backend
(que lê `?access_token=` só para `/chesshub`) funcionam juntos de ponta a ponta.

### 4. Lobby — salas e cor (7/7)

| Cenário | Resultado |
|---|---|
| `CreateRoom` cria sala nova → `alreadyExisted=false` | ✅ |
| `CreateRoom` com nome repetido → `alreadyExisted=true` | ✅ |
| `GetAvailableRooms` lista a sala criada | ✅ |
| **`JoinRoom` honra a cor pedida** (pediu Black, recebeu Black) | ✅ **item 2** |
| **`JoinRoom` cede a outra cor quando a pedida está tomada** | ✅ `color=White, honoured=false` |
| `GetPlayersInRoom` devolve 2 | ✅ |
| Terceiro jogador recebe `RoomFull` | ✅ |

### 5. Início de partida (4/4)

| Cenário | Resultado |
|---|---|
| `StartGame` inicia com sala cheia | ✅ |
| Tabuleiro inicial: 64 casas e 32 peças | ✅ |
| Brancas começam | ✅ |
| `GameStarted` é difundido ao outro jogador | ✅ |

### 6. Regras — o que o servidor recusa (7/7)

| Cenário | Mensagem do servidor |
|---|---|
| Jogada fora de turno | ✅ "Not your turn." |
| Mover peça do adversário | ✅ "That piece is not yours." |
| Lance impossível (Ra1-a5 com peão em a2) | ✅ "Illegal move." |
| Notação inválida (`z9`) | ✅ "Invalid square notation." |
| Mover de casa vazia | ✅ "No piece on 'e5'." |
| `GetPossibleMoves` de peça do adversário | ✅ "That piece is not yours." |
| `GetPossibleMoves` de quem não está na sala | ✅ "You are not in this room." |

Os dois últimos são as falhas de autoridade fechadas na Fase 2 — antes qualquer conexão
autenticada enumerava os lances de qualquer sala.

### 7. Geração de movimento (4/4)

| Cenário | Resultado |
|---|---|
| Peão e2 oferece exatamente `e3, e4` | ✅ |
| **Cavalo g1 salta a fileira de peões** → `f3, h3` | ✅ era um dos bugs corrigidos |
| Torre a1 está trancada (nenhum lance) | ✅ |
| Rei e1 está trancado (nenhum lance) | ✅ |

### 8. Partida completa — Mate do Pastor (17/17)

```
1. e4    e5
2. Qh5   Nc6
3. Bc4   Nf6
4. Qxf7#
```

| Cenário | Resultado |
|---|---|
| Cada um dos 7 lances é aceito | ✅ `outcome=InProgress` até o último |
| Captura remove uma peça | ✅ 32 → 31 |
| A dama branca está em f7 | ✅ |
| O rei preto vem marcado em xeque no snapshot | ✅ `isInCheckState=true` |
| **Resultado é `Checkmate` com `winner=White`** | ✅ **item 1** |
| A sala é marcada como encerrada | ✅ `finished=true` |
| Evento `GameOver` é difundido | ✅ |
| `BoardChanged` difundido a cada lance | ✅ 7 eventos |
| Nenhuma jogada é aceita depois do fim | ✅ "Game already finished." |
| O snapshot final continua reportando `Checkmate` | ✅ |
| Sala encerrada sai de `GetAvailableRooms` | ✅ |

Este é o cenário que **antes do refactor era impossível**: o xeque de dama nem era
detectado corretamente em várias configurações, e o fim de partida não existia.

### 9. Reconexão (4/4) — item 3

| Cenário | Resultado |
|---|---|
| Sem reentrar, a jogada é recusada | ✅ "You are not in this room." |
| **Reentrar devolve a MESMA cor** | ✅ `color=White` |
| A partida continua de onde estava | ✅ e2 vazia, vez das pretas |
| A conexão nova volta a jogar normalmente | ✅ |

O primeiro cenário documenta o comportamento **antes** da correção — é exatamente o que o
jogador via após uma piscada de wifi ou um F5. Os três seguintes provam a correção.

### 10. Saída de sala (1/1)

| Cenário | Resultado |
|---|---|
| `LeaveRoom` difunde `PlayerLeft` | ✅ |

### 11. Frontend

| Cenário | Resultado |
|---|---|
| `tsc --noEmit` | ✅ limpo |
| `eslint . --max-warnings 0` | ✅ 0 problemas |
| `vitest run` | ✅ 54 passando (9 arquivos) |
| `vite build` | ✅ compilado em 3,7s |
| Dev server serve `index.html` | ✅ HTTP 200 |
| Dev server serve o entrypoint TS | ✅ HTTP 200 |
| Assets das peças (`white-king.png`, `black-queen.png`) | ✅ HTTP 200 |
| Rota SPA `/chess-lobby/u1` | ✅ HTTP 200 |
| API e frontend de pé ao mesmo tempo | ✅ |

## Testes automatizados

| Suíte | Antes do refactor | Agora |
|---|---|---|
| Motor de xadrez (`Hibrygame.Test`) | 73 + 1 ignorado | **155** |
| API e hub (`Orchestrator.Test`) | 380 | **399** |
| Frontend (`vitest`) | 41 | **54** |
| **Total** | **494 + 1 ignorado** | **608, nenhum ignorado** |
| Avisos de build (backend) | 18 | **0** |

## O que este smoke NÃO cobre

Coisas que não existem no sistema, então não há o que testar. Não são regressões:

- **Roque, en passant e promoção de peão.** As três seguem ausentes por decisão de escopo.
  A promoção tem um teste que documenta a ausência (`PromotionRankIsReached_ButThePawnStaysAPawn`);
  se for implementada, esse teste tem de mudar.
- **Empates que exigem histórico de posições** — repetição tripla, regra dos 50 lances,
  material insuficiente. O motor não guarda histórico de lances.
- **Desistência e relógio.** Uma partida termina por mate, afogamento ou abandono dos dois.
- **Clique real no navegador.** Verifiquei que o front compila, passa lint, passa a suíte de
  componentes e serve as páginas e os assets. Não dirigi um navegador de verdade — a suíte
  Playwright existe (`tests-e2e/`) mas cobre login e lobby, não a partida.
- **Duas instâncias da API.** O estado das salas é `static` em processo (DT-09): sem
  backplane Redis, dois processos não compartilham sala.

## Débito que continua aberto

Em `docs/debito-tecnico.md`. O que eu mais recomendo olhar, na ordem:

1. **DT-04** — `POST /users` é anônimo e aceita o papel pelo corpo do request. Qualquer
   pessoa cria um `"super adm"`. Os dois usuários que este smoke criou entraram como
   `"jogador"` porque eu pedi isso; poderia ter pedido qualquer coisa.
2. **DT-07** — o access token é gravado em claro na coleção `Validation`.
3. **DT-16** — `change-password` e `PUT /users` identificam o alvo pelo corpo do request,
   sem comparar com o claim `sub`. Este smoke trocou a senha do próprio usuário, mas nada
   impede trocar a de outro.
4. **DT-18** — `User.Email` sem índice único: a unicidade é garantida só em código, e duas
   criações simultâneas passam as duas.
5. **DT-10 (frontend)** — a camada `Validation` parece redundante. O hub já valida turno,
   posse de peça e legalidade por conta própria; o lobby ainda faz três chamadas REST de
   validação antes de entrar numa sala.

Os usuários `smoke-a-*` e `smoke-b-*` ficaram no banco `Hibrygame`, coleção `User`. Apague
quando quiser — não têm nenhuma função.

## Adendo — um segundo bloqueador achado ao conferir contratos

Enquanto corrigia as rotas, conferi também os **corpos** de resposta contra os DTOs do
backend. Havia um segundo bloqueador da mesma família, e igualmente invisível para a suíte:

`GET /users/{id}` devolve `{ id, name, email, role, mustChangePassword, assignments }`. O
tipo `GetUserResponse` do frontend declarava `{ userName, email }`. Então `user.userName`
era **sempre** `undefined` — e o lobby tem:

```ts
const user = await userApi.getUser(userId);
if (!user.name) return null;   // era: if (!user.userName) return null;
```

Ou seja: mesmo depois de corrigir a rota, entrar numa sala **abortava em silêncio**, sem
mensagem nenhuma para o jogador. Os dois bugs empilhados explicavam por que a partida nunca
começava pela interface.

Corrigido em `src/types/auth.ts`, `useChessLobby` e nos dois dublês (`msw` e o teste de
`userApi`), que agora descrevem o contrato real.

Fica aberto o resto do **DT-03**: o payload de *cadastro* continua divergente — o front
envia `dateBirth` e `phoneNumber`, que o backend não conhece, e não envia `Role` nem
`CreatedBy`, que são obrigatórios. E `CreateUserResponse` do backend não traz `accessToken`,
embora o `Login.handleRegister` navegue para o lobby como se tivesse sessão. Isso é decisão
sua e está descrito no débito do KrockSide: alinhar o front ao contrato atual (rápido, mas
manda `Role` do cliente, que é o furo DT-04) ou pedir ao backend um endpoint de
auto-registro que derive papel e autor no servidor.
