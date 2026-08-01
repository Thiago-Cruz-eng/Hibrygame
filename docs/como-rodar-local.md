# Como rodar local

Verificado em 2026-08-01 nesta máquina, com a pilha inteira de pé ao mesmo tempo.

## Usuários já criados

Senha igual para os três: **`Xadrez@2026`**

| E-mail | Nome | Papel | Para que serve |
|---|---|---|---|
| `branca@hibrygame.local` | Branca | `jogador` | um lado da partida |
| `preta@hibrygame.local` | Preta | `jogador` | o outro lado |
| `admin@hibrygame.local` | Administrador | `super adm` | `POST /users`, `DELETE /users/{id}` |

Apaguei os 15 usuários de teste que os smokes deixaram no banco — o e-mail deles era
ilegível (`smoke-a-639212086066552225@…`). Estes três são os únicos que existem.

O `admin` foi criado como `jogador` pelo auto-registro e promovido depois no Mongo, que é o
único caminho para o **primeiro** administrador:

```bash
docker exec xadrez mongosh --quiet Hibrygame --eval \
  'db.User.updateOne({Email:"admin@hibrygame.local"},{$set:{Role:"super adm"}})'
```

## 1. MongoDB

Já está de pé (container `xadrez`, MongoDB 8.2.12, sem autenticação). Se precisar recriar:

```bash
# no WSL — o Docker desta máquina vive lá, não no Windows
docker run -d --name xadrez -p 27017:27017 -v xadrez-data:/data/db \
  --restart unless-stopped mongo:latest
```

O `-p 27017:27017` é obrigatório. Sem ele a porta fica apenas *exposta* e nada no Windows
alcança — nem o Compass, nem a API.

Compass: `mongodb://localhost:27017`, banco `Hibrygame`.

## 2. Backend

```bash
cd C:\Users\THIAGO.CRUZ\Documents\GitHub\Hibrygame
dotnet run --project Orchestrator --no-launch-profile --urls "http://localhost:5001"
```

Swagger em <http://localhost:5001/swagger>.

**Por que `--no-launch-profile` e `--urls`:** o `launchSettings.json` usa as portas 5231
(http) e 7111 (https), que não são as que o frontend espera. E HTTPS exigiria certificado de
desenvolvimento confiável (`dotnet dev-certs https --trust`, que abre prompt do Windows).
HTTP na 5001 evita o passo e funciona igual em desenvolvimento — o `UseHttpsRedirection`
apenas registra um aviso e deixa passar.

Se preferir HTTPS, rode `dotnet dev-certs https --trust` uma vez, use
`--urls "https://localhost:5001"` e apague o `.env` do frontend (o padrão do código já é
`https://localhost:5001/`).

## 3. Frontend

```bash
cd C:\Users\THIAGO.CRUZ\Documents\GitHub\KrockSide
"C:\Program Files\nodejs\npm.cmd" start
```

<http://localhost:3000>.

**Use o caminho completo do npm.** Nesta máquina o `npm` do PATH está quebrado: `NVM_HOME`
e `NVM_SYMLINK` apontam para `C:\Users\dgs-admin\AppData\Local\nvm` — perfil de outro
usuário — e `C:\nvm4w\nodejs` vem antes de `C:\Program Files\nodejs` no PATH. `npm` puro
morre com `EPERM`. Vale arrumar o PATH ou reinstalar o nvm no seu perfil.

Criei um `.env` (ignorado pelo git) apontando para a API em HTTP:

```
VITE_API_BASE_URL=http://localhost:5001/
VITE_HUB_URL=http://localhost:5001/chesshub
```

A porta 3000 é fixada em `vite.config.ts` com `strictPort`, e não é preferência: o CORS do
backend libera exatamente `http://localhost:3000`. Se o Vite subir noutra porta, o login
falha por CORS.

## 4. Jogar uma partida

Precisa de **duas sessões separadas** — o estado de sessão (cor atribuída, nome do jogador)
vive em `sessionStorage`, que é por aba. Duas abas normais do mesmo navegador funcionam;
uma normal + uma anônima também.

1. **Aba 1** — login com `branca@hibrygame.local`
2. Escolha uma cor (Branco ou Preto) — o botão "Entrar na Sala" fica desabilitado até
   escolher
3. "Criar Novo Jogo", dê um nome, "Criar Jogo"
4. "Entrar na Sala". Você vai para o tabuleiro e vê **"Aguardando adversário…"**
5. **Aba 2** — login com `preta@hibrygame.local`, escolha a cor, entre na **mesma sala**
6. A partida começa sozinha quando o segundo entra

O servidor honra a cor pedida quando ela está livre. Se os dois pedirem a mesma, o segundo
recebe a outra e o lobby avisa: *"A cor X já estava tomada — você joga de Y."*

**Não dá para ir direto pela URL** `/chess-board/:sala/:id`: o tabuleiro precisa da cor que
o servidor atribuiu no `JoinRoom`, e ela é guardada ao passar pelo lobby.

### Para ver o fim de partida rápido

Mate do Pastor, quatro lances das brancas:

```
1. e2-e4    e7-e5
2. d1-h5    b8-c6
3. f1-c4    g8-f6
4. h5-f7    → xeque-mate
```

O tabuleiro anuncia "Xeque-mate — você ganhou!" para as brancas, e trava: com a partida
encerrada nenhum clique ou arrasto é aceito.

## Se algo não funcionar

| Sintoma | Causa provável |
|---|---|
| Login dá erro de rede | API não está de pé, ou está noutra porta que não a 5001 |
| Login dá 401 com a senha certa | senha é `Xadrez@2026`, com `X` maiúsculo e `@` |
| Erro de CORS no console | o Vite não está na 3000 |
| `npm` morre com `EPERM` | use `"C:\Program Files\nodejs\npm.cmd"` |
| Tabuleiro carrega mas nenhum clique funciona | não é mais o bug antigo — verifique se você passou pelo lobby nesta aba |
| API não sobe, erro sobre `Jwt:Key` | a chave precisa de 32+ bytes; a validação na subida diz o tamanho encontrado |

Logs úteis: a API escreve no console; o Mongo responde a
`docker exec xadrez mongosh --quiet Hibrygame --eval 'db.User.countDocuments()'`.
