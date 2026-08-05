---
name: persistencia-mongodb
description: >
  Persistência MongoDB do Orchestrator: IGenericRepository e seus métodos, BaseRepositoryNoSql<T>
  e repositórios concretos por entidade, resolução de coleção por CollectionNameAttribute,
  Guid e DateTime serializados como string, ausência de migration e de transação, e as
  armadilhas de Update com params e de Delete que ignora a entidade. Use ao criar entidade
  nova, adicionar repositório, escrever query, filtro, projeção ou paginação, mexer em
  GenericRepository, MongoDbContext ou registro de serializer.
metadata:
  type: technical-skill
---

# Persistência MongoDB

> **Mantendo esta skill**
>
> Atualize sempre que a camada de acesso a dados mudar de contrato. Refactor interno do
> `GenericRepository` que preserve as assinaturas não exige mudança.

## Visão geral do padrão

Três camadas, sem ORM e sem migration:

```
IMongoDbContext            expõe IMongoDatabase (Orchestrator/Infra/Mongo/)
IGenericRepository         CRUD tipado genérico sobre qualquer BaseEntity
BaseRepositoryNoSql<T>     fachada estreita por entidade, delega ao genérico
{Entidade}RepositoryNoSql  classe concreta + I{Entidade}RepositoryNoSql
```

Registro em `Program.cs`:

```csharp
builder.Services.AddSingleton<IMongoClient>(...);       // connection string
builder.Services.AddSingleton<IMongoDbContext>(...);    // client.GetDatabase(nome)
builder.Services.AddScoped<IGenericRepository, GenericRepository>();
builder.Services.AddScoped<IUserRepositoryNoSql, UserRepositoryNoSql>();
```

## Entidade

Toda entidade persistida herda `BaseEntity` (`Id : Guid` com `[BsonId]`,
`[BsonIgnoreIfDefault]`, default `Guid.NewGuid()`) e declara a coleção por atributo:

```csharp
[CollectionName(nameof(User))]
public class User : BaseEntity { ... }
```

`GenericRepository.ResolveCollectionName<T>()` lê o atributo por reflection e cai no
`typeof(T).Name` se não houver. **Sempre declare o atributo** — depender do fallback amarra o nome
da coleção ao nome da classe e uma renomeação vira perda de dados silenciosa.

Convenções de invariante (setters protected, factory `Create`, mutador nomeado, auditoria) estão
na skill `casos-de-uso-e-api-http` e no Princípio IV da constituição.

### Serialização

Registrado globalmente no início do `Program.cs`, **antes** de qualquer uso:

```csharp
BsonSerializer.RegisterSerializer(new GuidSerializer(BsonType.String));
BsonSerializer.RegisterSerializer(new DateTimeSerializer(BsonType.String));
BsonSerializer.RegisterSerializer(new DateTimeOffsetSerializer(BsonType.String));
```

Então `Guid` e datas são **strings** no banco, não `UUID`/`ISODate`. Implicações:

- filtro por data ordena lexicograficamente na consulta crua — evite range query em `DateTime` sem
  testar;
- documento gravado por outra ferramenta com `UUID` binário não desserializa;
- `RegisterSerializer` é global e só pode ser chamado uma vez por tipo: não repita em teste nem em
  outro ponto de startup.

Campo novo em entidade existente entra **opcional** ou com `[BsonIgnoreIfNull]` — não há migration
para preencher documentos antigos.

## `IGenericRepository`

| Método | Nota |
|---|---|
| `GetCollection<T>()` | acesso cru ao `IMongoCollection<T>`; escape hatch |
| `GetAll<T>(filter?, skip, limit, sort?, ct)` | `filter` nulo vira `x => true` |
| `GetFirstOrDefault<T>(filter, ct)` | usa `Aggregate().Match(...)`, não `Find` |
| `GetFirstProjectedOrDefault<T,TDest>(projection, filter?, ct)` | projeção por expressão |
| `GetProjected<T,TDest>(...)` / `GetProjectedPaginated<T,TDest>(...)` | paginação é `skip = (page-1)*pageSize` |
| `CountAsync<T>(filter, ct)` | |
| `Save<T>` / `SaveAndReturn<T>` / `SaveMany<T>` | `InsertOne`/`InsertMany` — **só inserção** |
| `ReplaceOne<T>(filter, record, ct)` | `true` só se `ModifiedCount > 0` |
| `SaveOrReplaceOne<T>(filter, obj, ct)` | tenta replace, insere se não modificou |
| `DeleteOne<T>` / `DeleteMany<T>` | `true` se `DeletedCount > 0` |
| `HasRecord<T>(filter, ct)` | `CountAsync > 0` |
| `Update<T>(filter, ct, params (Expression, object)[])` | `$set` direcionado |

Armadilhas reais:

- **`ReplaceOne` devolve `false` quando o documento não mudou**, não só quando não existe:
  `ModifiedCount == 0` para replace idempotente. Não trate `false` como "não encontrado".
- **`SaveOrReplaceOne` insere quando o replace não modifica nada** — replace idempotente vira
  documento duplicado. Prefira `ReplaceOne` + checagem explícita, ou `Update` direcionado.
- **`Update<T>` tem `CancellationToken` antes do `params`**: a chamada é
  `Update(filter, cancellationToken, (x => x.Campo, valor))`. Fácil de errar; passe o `ct`
  explicitamente.
- `Save` é `InsertOne`: chamar duas vezes com o mesmo `Id` lança duplicate key. Para upsert use
  `SaveOrReplaceOne` ciente da armadilha acima.
- `GetAll` com `limit = int.MaxValue` por padrão — nenhuma query é paginada por acidente. Passe
  `limit` em qualquer caminho que possa crescer.

## Repositório por entidade

```csharp
public class UserRepositoryNoSql : BaseRepositoryNoSql<User>, IUserRepositoryNoSql
{
    public UserRepositoryNoSql(IGenericRepository genericRepository) : base(genericRepository) { }
}
```

`BaseRepositoryNoSql<T>` expõe só cinco operações (`IGenericRepositoryNoSql<T>`):
`FindByFilter`, `GetById`, `Save`, `Update(string id, T)`, `Delete(string id, T)`.

Notas:

- `Update`/`Delete` recebem `id` como **string** e devolvem `false` se `Guid.TryParse` falhar —
  falha silenciosa, sem exceção. Valide o id antes se a distinção importar.
- `Delete(string id, T entity)` **ignora** o parâmetro `entity`: deleta por `Id`. A assinatura
  engana; não passe a entidade esperando delete condicional.
- `FindByFilter` devolve `IEnumerable<T>` já materializado (`GetAll` faz `ToListAsync`) — não é
  query lazy, todo filtro tem de estar na expressão.
- Só adicione método ao repositório concreto quando a entidade precisar de query além disso; caso
  contrário a classe fica vazia mesmo (é o padrão, não é código incompleto).

Interfaces (`IUserRepositoryNoSql : IGenericRepositoryNoSql<User>`) existem para o caso de uso
depender de abstração e para o teste mockar com Moq. **Caso de uso nunca injeta
`IGenericRepository` direto.**

## Entidades e coleções atuais

| Entidade | Coleção | Papel |
|---|---|---|
| `User` | `User` | usuário, papel primário e `Assignments` embutidos |
| `RefreshToken` | `RefreshToken` | token rotativo hasheado, cadeia de revogação |
| `Validation` | `Validation` | sessão de jogo (token, sala, cor) — ver skill `validacao-de-sessao-de-jogo` |

`UserAssignment` e `HierarchyNode` são documentos **embutidos** em `User.Assignments`, sem coleção
própria e sem `BaseEntity`. `AuditInformation` (`CreationInformation`/`ModificationInformation`)
também é embutido, com propriedades somente-leitura — o Mongo desserializa por campo, não por
setter.

## Restrições

- **Sem transação.** Atomicidade só por documento. `RefreshTokenUseCase` faz `Update` do token
  antigo e `Save` do novo em duas operações: falha no meio deixa o antigo revogado sem substituto,
  e o usuário precisa logar de novo. Aceito hoje; qualquer fluxo novo que grave em duas coleções
  precisa ser idempotente ou tolerar o meio-caminho.
- **Sem índice declarado.** Nenhum índice é criado no startup — `User.Email` e
  `RefreshToken.UserId` são varredura de coleção. Ao criar índice, faça idempotente
  (`CreateOneAsync` é idempotente para a mesma definição) e documente onde roda.
- **Sem migration.** Ver acima.
- **`MongoDbContextFactory.CreateAsync(string country)`** é código morto herdado de outro projeto,
  com connection string hardcoded (DT-03). Não use como referência; não passe a usar.
- **Chave de configuração divergente:** `Program.cs` lê `Mongo:ConnectionString`/`Mongo:Database`,
  o `appsettings.json` declara `HibrygameDatabase:*`. Hoje sempre cai no fallback
  `mongodb://localhost:27017` / base `Hibrygame` (DT-06).
- A suíte de testes **não** sobe Mongo: todo repositório é mockado com Moq. Não escreva teste que
  dependa de banco real sem antes decidir a estratégia (não há Testcontainers no projeto).
