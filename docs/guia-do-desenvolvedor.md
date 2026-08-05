# Guia do desenvolvedor — backend

Guia de tarefa: **"quero fazer X, começo por onde?"**. Escrito para quem está chegando no
repositório agora.

Não repete o que já está escrito em outro lugar — quando o assunto tem dono, este guia aponta:

| Preciso de… | Está em |
|---|---|
| Subir MongoDB, backend e front na minha máquina | [`como-rodar-local.md`](como-rodar-local.md) |
| Nome de branch, onde o teste mora, como abrir PR | [`fluxo-de-trabalho.md`](fluxo-de-trabalho.md) |
| Entender um fluxo por dentro, com diagrama | [`ARCHITECTURE.md`](ARCHITECTURE.md) |
| Convenções obrigatórias e áreas críticas | [`../AGENTS.md`](../AGENTS.md) |
| O que já se sabe que está torto | [`debito-tecnico.md`](debito-tecnico.md) |
| Contrato consumido pelo front | [`FRONTEND_CHANGES.md`](FRONTEND_CHANGES.md) |

> **Uma coisa antes de tudo:** o código deste repositório é comentado de propósito, e os comentários
> explicam **por quê**, não o quê. Muitos citam o bug concreto que motivou a decisão. Quando você
> abrir um arquivo, leia o comentário do topo da classe antes de mexer — ele frequentemente contém
> exatamente o aviso que evitaria a próxima meia hora de investigação.

---

## 1. O mapa mental

Quatro camadas, e a dependência só aponta para baixo:

```
Presentation   Controllers. Traduzem HTTP <-> caso de uso. Nada mais.
      |
      v
UseCases       Uma classe por ação. Onde vive a regra de negócio.
      |
      v
Infra          MongoDB, hub SignalR, segurança. O "como".
      |
      v
Domain         Entidades. Protegem o próprio estado. Sem dependência externa.
```

E, ao lado, independente de tudo isso:

```
Hibrygame/     A engine de xadrez. C# puro, sem ASP.NET, sem Mongo, sem SignalR.
```

**A regra que não se quebra:** `Presentation` fala só com `UseCases`; `UseCases` fala só com
interfaces. Regra de xadrez entra na engine, com teste na engine — **nunca** no hub.

### Onde cada coisa mora

```
Hibrygame/Logic/              Engine: Board, Position, Piece + 6 peças, Move
Orchestrator/
  Domain/                     Entidades
  UseCases/                   Um caso de uso por ação
    Dto/Request/              Corpo que entra
    Dto/Response/             Corpo que sai
    Interfaces/               Contratos de serviço de aplicação
    Security/                 Hash, token, autorização por papel
  Infra/
    BaseRepository/           CRUD genérico do Mongo
    Repositories/             Um repositório por entidade
    Interfaces/               Contratos de repositório
    Mongo/                    Contexto do banco
    SignalR/                  ChessHub + GameRoom
    Settings/                 JwtSettings
    Utils/                    Atributo de coleção, extensões do driver
  Composition/                Registro de DI, um arquivo por assunto
  Presentation/               Controllers
  Program.cs                  Índice da composição + pipeline de requisição
```

Se você quer saber **como a aplicação é montada**, comece pelo `Program.cs`: ele é curto de
propósito e cada linha aponta para um arquivo de `Composition/`.

---

## 2. Receitas

### Receita 1 — Endpoint novo

Ordem importa. Fazendo nesta sequência, cada passo compila.

1. **DTO de entrada** em `UseCases/Dto/Request/{Ação}Request.cs`.
   Validação de **formato** vai em atributo (`[Required]`, `[EmailAddress]`, `[Compare]`) — eles são
   verificados automaticamente antes do controller rodar, porque o controller tem `[ApiController]`.
   Request inválido já volta 400 sem passar pelo caso de uso.

2. **DTO de saída** em `UseCases/Dto/Response/{Ação}Response.cs`, com `Success` e `Message`.
   Monte a resposta **campo a campo**. Nunca devolva a entidade: `User` tem `PasswordHash` e `Salt`.

3. **Caso de uso** em `UseCases/{Ação}{Entidade}UseCase.cs`. O molde é
   [`CreateUserUseCase`](../Orchestrator/UseCases/CreateUserUseCase.cs) — leia o comentário da
   classe, que descreve a anatomia esperada. O essencial:

   ```csharp
   public class FazerAlgoUseCase
   {
       private readonly IXRepositoryNoSql _repo;
       private readonly ILogger<FazerAlgoUseCase> _logger;

       public FazerAlgoUseCase(IXRepositoryNoSql repo, ILogger<FazerAlgoUseCase> logger)
       {
           _repo = repo;
           _logger = logger;
       }

       public async Task<FazerAlgoResponse> ExecutarAsync(FazerAlgoRequest req)
       {
           try
           {
               // regra de negócio aqui
               return new FazerAlgoResponse { Success = true, Message = "..." };
           }
           catch (Exception e)
           {
               _logger.LogError(e, "Error while ...");
               return new FazerAlgoResponse { Success = false, Message = "..." };
           }
       }
   }
   ```

   **Exceção nunca sobe para o controller.** Falha vira `Success = false`. E logue a exceção antes
   de convertê-la — sem isso a causa real desaparece.

4. **Registre** em `Composition/UseCaseComposition.cs`. Esquecer disto **compila** e falha só quando
   a requisição chega. É o erro mais comum de quem está começando aqui.

5. **Action no controller**, fina:

   ```csharp
   [Authorize(Policy = "Role:Player")]
   [HttpPost("/caminho")]
   public async Task<IActionResult> FazerAlgo([FromBody] FazerAlgoRequest req)
   {
       var result = await _fazerAlgoUseCase.ExecutarAsync(req);
       return result.Success ? Ok(result) : BadRequest(result);
   }
   ```

   Autorização **sempre por policy**: `[Authorize(Policy = "Role:X")]`.
   `[Authorize(Roles = "...")]` é proibido — ele compara papel por texto e ignora a hierarquia, então
   `Roles = "adm"` recusaria um `super adm`.

6. **Teste** em `Orchestrator.Test/`, e atualize `docs/FRONTEND_CHANGES.md` se o front consome.

### Receita 2 — Campo novo numa entidade

**Não existe migration neste projeto.** Todo campo novo tem de ser retrocompatível: opcional, ou com
valor padrão, ou `[BsonIgnoreIfNull]`. Documento já gravado não tem o campo, e ler um documento
antigo não pode falhar.

1. Propriedade com setter **`protected`** na entidade.
2. Se ela pode mudar depois de criada, um **mutador nomeado** que devolve `this` e preenche
   `ModificationInformations`. Não exponha `public set`.
3. Se ela é obrigatória na criação, acrescente ao `Create(...)`.
4. Ajuste os DTOs que trafegam a entidade.

O molde é [`User`](../Orchestrator/Domain/User.cs). O contra-exemplo é
[`Validation`](../Orchestrator/Domain/Validation.cs) — ela não segue o contrato, e o comentário dela
diz exatamente onde e por quê. Não copie a `Validation`.

### Receita 3 — Regra de xadrez nova

Tudo acontece em [`Hibrygame/Logic/Move.cs`](../Hibrygame/Logic/Move.cs), que é organizado em três
camadas e tem a explicação no topo do arquivo. Resumo:

| Camada | Responde | Quem consulta |
|---|---|---|
| `AttackedSquares` | que casas a peça ataca, geometria pura | a detecção de xeque |
| `CandidateMoves` | geometria + ocupação do destino | `LegalMovesFor` |
| `LegalMovesFor` | candidatos menos os que expõem o próprio rei | `Piece.GetPossibleMove` |

**A invariante que não se quebra:** gerar lances é **leitura**. A avaliação de legalidade simula
cada lance no tabuleiro e **sempre desfaz**. Existe um teste que garante isso
(`GetPossibleMove_ForEveryPieceOnTheBoard_NeverChangesThePosition`) — se ele ficar vermelho, é isso
que você quebrou.

Duas coisas que economizam tempo:

- **As classes de peça não têm geometria.** `Pawn`, `Knight` etc. guardam só cor e tipo. Não crie
  lista de direção fora de `Move` — ela já existiu em dois lugares, e mudar um e esquecer o outro
  era o bug esperado.
- **Campo novo em peça precisa entrar no rollback** de `Move.MakeMove`. Sem isso, o lance recusado
  deixa estado sujo no tabuleiro.

### Receita 4 — Método novo no hub que altera o tabuleiro

Duas obrigações, as duas não negociáveis:

**1. Repita as seis checagens de autoridade do servidor.** O cliente manda só "de onde" e "para
onde"; tudo o mais é reconferido:

```
partida em andamento -> identidade (a conexão tem assento nesta sala?)
  -> notação válida -> turno -> posse da peça -> legalidade recalculada no servidor
```

Lista de lances vinda do cliente é **ignorada**. O molde é
[`ChessHub.MakeMove`](../Orchestrator/Infra/SignalR/ChessHub.cs), que está dividido em
`MakeMove` (guardas baratas) → `ApplyMove` (sob o lock) → `BroadcastMoveAsync` (difusão).

**2. Toque o tabuleiro só dentro de `GameRoom.Serialized`.** O tabuleiro é compartilhado e a
avaliação de legalidade simula lances nele. Duas chamadas concorrentes sem esse lock corrompem o
estado — já apareceu peão branco em `a2`, `a3` e `a4` ao mesmo tempo, com 34 peças no tabuleiro.

Difusão (`Clients.Group(...).SendAsync`) fica **fora** do lock: é rede, não toca o tabuleiro, e
prender o lock por uma ida à rede segura os outros jogadores da sala.

### Receita 5 — Repositório novo

Três arquivos, e todos são quase vazios — o CRUD vem da base:

```csharp
// Infra/Interfaces/IPecaRepositoryNoSql.cs
public interface IPecaRepositoryNoSql : IGenericRepositoryNoSql<Peca> { }

// Infra/Repositories/PecaRepositoryNoSql.cs
public class PecaRepositoryNoSql : BaseRepositoryNoSql<Peca>, IPecaRepositoryNoSql
{
    public PecaRepositoryNoSql(IGenericRepository generic) : base(generic) { }
}
```

E registre o par em `Composition/PersistenceComposition.cs`.

**Query específica de uma entidade entra na classe concreta** — não em `BaseRepositoryNoSql` (que é
compartilhada por todas) nem em `IGenericRepository` (que não conhece entidade alguma).

---

## 3. As armadilhas deste repositório

Estas custam tempo de quem não sabe. Todas estão comentadas no código também.

### Coordenadas do tabuleiro estão com os nomes trocados

- `Row` (0..7) é o **arquivo** — a coluna `a`..`h`.
- `Column` (0..7) é a **fileira** — e numerada ao contrário: `Column = 0` é a fileira **8**,
  `Column = 7` é a fileira **1** (onde ficam as brancas).

Conversão: `arquivo = 'a' + Row`, `fileira = 8 - Column`.

**Nunca faça essa conta à mão.** Use `Position.FromAlgebraic`, `Position.TryFromAlgebraic` e
`Position.Algebraic`. A fronteira externa (hub, DTO, log) fala sempre `"e2"`; `Row`/`Column` são
internos da engine.

### O estado do hub é `static`

`ChessHub` guarda as salas num `ConcurrentDictionary` **estático**, compartilhado por todo o processo
— **e por toda a suíte de testes**. Consequências:

- Teste novo de hub usa nome de sala único: `$"test-{Guid.NewGuid()}"`.
- Não há escala horizontal: duas instâncias da API não compartilham sala. Precisaria de backplane
  Redis.

### `appsettings.json` é decorativo para o Mongo

`Program.cs` lê `Mongo:ConnectionString` e `Mongo:Database`; o arquivo declara
`HibrygameDatabase:*`. As chaves nunca casam, então **o fallback é sempre o que vale**
(`mongodb://localhost:27017`, base `Hibrygame`). É o DT-06. Se você mudar a connection string no
`appsettings` e "não pegar", é isto.

### Os claims do token não são renomeados — e isso é de propósito

`Program.cs` liga `MapInboundClaims = false`. Sem isso o handler renomeia `sub` para
`ClaimTypes.NameIdentifier`, e todo `User.FindFirst(JwtRegisteredClaimNames.Sub)` devolve `null` —
o que já fez os quatro endpoints de `/validation` responderem 403 para todo mundo, sempre.

### `Update` vs `ReplaceOne`

- `IGenericRepositoryNoSql.Update(id, entity)` **substitui o documento inteiro**. Campo que exista no
  banco e não na instância desaparece.
- `IGenericRepository.Update(filter, ct, campos...)` altera **só os campos listados**.

E os dois devolvem `false` quando o documento encontrado já era idêntico ao enviado — o Mongo não
conta isso como modificação. **Não use esse retorno como prova de que o documento não existe.**

### Detalhes herdados que parecem bug (e são)

- `"Same error happen"` é a mensagem de falha inesperada em quatro casos de uso. É erro de inglês
  (seria *"Some error happened"*), preservado porque está registrado como convenção numa skill.
- `AcessToken` (um `c`) em `Validation` e `ValidationDto`. Não pode ser renomeado sem migrar dados:
  o nome da propriedade é o nome do campo no MongoDB.
- `UserAssignment` recebe `createdBy`/`modifiedBy` em **todos** os métodos e **ignora os dois** — a
  classe não tem campo de auditoria. Alteração de vínculo não deixa rastro.
- `PUT /users/{id}` devolve **404** para qualquer falha, inclusive `"Email already in use"` e
  `"Invalid role"`, que são 400 por natureza.
- `ValidationController.Verify` e `.Get` testam `validation is null`, mas
  `GetValidationByUserToken` **lança** exceção quando não encontra. Os testes de `null` passam só
  porque o mock devolve algo que a implementação real nunca devolve — na ausência de validação, o
  endpoint responde **500**.

Encontrou outra? Registre em [`debito-tecnico.md`](debito-tecnico.md) em vez de corrigir de lado.

---

## 4. Testes

```bash
dotnet test                       # tudo
dotnet test Hibrygame.Test        # só a engine
dotnet test Orchestrator.Test     # só a API
```

Não precisa de MongoDB nem de Docker: os repositórios são mockados com Moq.

Nome do teste: **`{Método}_{Cenário}_{Resultado}`**, estrutura AAA (Arrange, Act, Assert).

| Testando | Como |
|---|---|
| Peça / engine | monte o tabuleiro direto, sem hub |
| Caso de uso | mocke os repositórios e `ISecureHashingService` / `ITokenService` |
| Hub | mocke `IHubCallerClients`, `IGroupManager`, `HubCallerContext`; **nome de sala único** |
| Controller | mocke `IAuthenticationService` em `RequestServices`, para `GetTokenAsync` resolver |
| `TokenService` | decodifique o payload por Base64, não pela biblioteca |

**Cuidado com mock que mente.** O caso de `ValidationController` acima é o exemplo: configurar o
mock para devolver `null` onde a implementação real lança faz o teste passar e o bug sobreviver. Ao
mockar, confira o que o método real **realmente** faz em caso de ausência — devolve `null`, lança,
ou devolve lista vazia?

---

## 5. Antes de pedir review

Detalhe completo em [`fluxo-de-trabalho.md`](fluxo-de-trabalho.md). O mínimo:

- [ ] `dotnet build` — **0 erros e 0 warnings**. Warning quebra o build neste repositório de
      propósito: `Directory.Build.props` liga `TreatWarningsAsErrors`, e warning em C# muitas vezes
      **é** bug (resultado descartado, possível desreferência nula, `async` sem `await`).
- [ ] `dotnet test` — tudo verde, nenhum ignorado.
- [ ] Camadas respeitadas: `Presentation` só chama `UseCases`; regra de xadrez está na engine.
- [ ] Método de hub que altera tabuleiro faz as seis checagens e está dentro do lock.
- [ ] Mudou contrato de front? → `docs/FRONTEND_CHANGES.md` (append, com data).
- [ ] Criou ou resolveu débito? → `docs/debito-tecnico.md`.
- [ ] Mudou convenção ou estrutura? → `AGENTS.md` + `README.md` + `.claude/CLAUDE.md`.

O `.editorconfig` da raiz cuida de formatação e nomenclatura automaticamente no Rider, no Visual
Studio e no VS Code. As regras de estilo são **dica**, não erro — de propósito, para não travar
quem está no meio de uma alteração. O que quebra o build é warning de compilador.

---

## 6. Onde não mexer sem conversar

| Arquivo | Por quê |
|---|---|
| `Hibrygame/Logic/Move.cs` | fonte única da geometria; a invariante "gerar lance é leitura" é frágil |
| `GameRoom.Serialized` | o lock que impede a corrupção do tabuleiro compartilhado |
| `ChessHub` (estado `static`) | compartilhado por todo o processo e por toda a suíte |
| `UserController.CreateUser` | superfície de escalada de privilégio (DT-04) |
| `ValidationService` | subdomínio com decisão pendente de existir ou não; superfície de segurança |
| `Program.cs` + `Composition/` | ordem dos middlewares muda comportamento em tudo |

Nesses casos: leia a skill correspondente em [`../.agents/skills/`](../.agents/skills/) **antes**.
Ela tem precedência sobre padrão inferido do código.
