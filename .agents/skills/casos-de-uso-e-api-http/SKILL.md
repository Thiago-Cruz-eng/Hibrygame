---
name: casos-de-uso-e-api-http
description: >
  Convenções de caso de uso, controller, DTO e entidade de domínio do Orchestrator: uma classe
  por ação com try/catch e ILogger retornando Response { Success, Message }, controller fino que
  traduz Success em status HTTP, DTOs em Dto/Request e Dto/Response, entidade com setters
  protected, factory Create, mutador nomeado e auditoria, e registro manual de DI no Program.cs.
  Use ao criar endpoint, caso de uso, DTO ou entidade, alterar rota, registrar serviço no DI
  ou revisar tratamento de erro da API.
metadata:
  type: technical-skill
---

# Casos de uso e API HTTP

> **Mantendo esta skill**
>
> Atualize sempre que o padrão de camada de aplicação mudar. Adicionar mais um caso de uso que
> siga o padrão não exige mudança aqui.

## Visão geral do padrão

Fluxo `Domain → UseCases → Infra → Presentation`, com um caso de uso por ação de negócio e
nenhuma exceção chegando ao controller.

```
Presentation/{Entidade}Controller.cs   fino: DTO → caso de uso → status HTTP
UseCases/{Ação}{Entidade}UseCase.cs    a ação, com try/catch e log
UseCases/Dto/Request/{Ação}Request.cs
UseCases/Dto/Response/{Ação}Response.cs
UseCases/Interfaces/I{Nome}Service.cs  contratos de serviço de aplicação
Domain/{Entidade}.cs                   invariantes e auditoria
Program.cs                             todo o registro de DI, manual
```

## Caso de uso

Uma classe, um método público, injeção por construtor, sem herança:

```csharp
public class CreateUserUseCase
{
    private readonly IUserRepositoryNoSql _userRepository;
    private readonly ISecureHashingService _hashingService;
    private readonly ILogger<CreateUserUseCase> _logger;

    public CreateUserUseCase(
        IUserRepositoryNoSql userRepository,
        ISecureHashingService hashingService,
        ILogger<CreateUserUseCase> logger)
    {
        _userRepository = userRepository;
        _hashingService = hashingService;
        _logger = logger;
    }

    public async Task<CreateUserResponse> CreateAsync(CreateUserRequest req)
    {
        try
        {
            // validações de negócio devolvem Success = false com Message específica
            if (!RoleHierarchy.TryGetLevel(req.Role, out var roleLevel))
                return new CreateUserResponse { Message = "Invalid role", Success = false };

            // ... regra ...
            return new CreateUserResponse { Success = true, Message = "User created", UserId = ... };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while creating user.");
            return new CreateUserResponse { Message = "Same error happen", Success = false };
        }
    }
}
```

Regras:

- **Nunca deixe exceção subir.** `try/catch (Exception e)` + `_logger.LogError(e, "...")` +
  retorno de falha. É o Princípio III da constituição.
- **`ILogger<T>` sempre injetado** — mesmo quando o caso de uso é simples. Caso de uso sem logger
  é o defeito de `ValidationService` (DT-05), não o padrão.
- **Mensagem de falha de negócio é específica; mensagem de exceção é genérica.** "Invalid role" vs
  "Login failed" — nunca vaze detalhe de exceção no `Message`, ele vai para o cliente.
- **Só interfaces nas dependências**: `I{Entidade}RepositoryNoSql`, `ITokenService`,
  `ISecureHashingService`, `IValidationService`. Nunca `GenericRepository` ou `IMongoDbContext`
  direto.
- **Nome do método varia** (`CreateAsync`, `LoginAsync`, `RefreshAsync`, `ChangeAsync`,
  `GetAsync`, `UpdateAsync`, `DeleteAsync`) — siga o verbo da ação, não force `ExecuteAsync`.
- **Normalização acontece no caso de uso**: e-mail com `Trim().ToLowerInvariant()`, papel com
  `RoleHierarchy.NormalizeRole`. Não confie no cliente e não normalize no controller.

## Response e Request

```csharp
public class CreateUserResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = null!;
    public string? UserId { get; set; }
}
```

Todo `Response` tem `Success` e `Message`; o resto é específico. Todo `Request` usa
`DataAnnotations` para validação de forma (`[Required]`, `[EmailAddress]`,
`[Compare("Password")]`) — validação de forma no DTO, validação de negócio no caso de uso.

Um arquivo por DTO, exceto quando a família é pequena e coesa
(`ValidationRequests.cs`/`ValidationResponses.cs` agrupam quatro cada). Não crie DTO
compartilhado entre ações só para reaproveitar campo.

## Controller

```csharp
[ApiController]
[Route("api/v1/[controller]")]
public class UserController : ControllerBase
{
    private readonly CreateUserUseCase _createUserUseCase;
    public UserController(CreateUserUseCase createUserUseCase) { ... }

    [Authorize(Policy = "Role:TeamLeader")]
    [HttpPut("/users/{id}")]
    [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(UpdateUserResponse))]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserRequest req)
    {
        var result = await _updateUserUseCase.UpdateAsync(id, req);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
```

- **Injete o caso de uso concreto** (não há interface para caso de uso) por construtor. Um
  controller pode injetar vários.
- **Zero regra de negócio.** Traduza `Success` em status: `Ok`, `Unauthorized`, `NotFound`,
  `Forbid`. O corpo é sempre o `Response` completo, inclusive na falha.
- **Autorização por policy** (`[Authorize(Policy = "Role:X")]`), no controller ou na action.
  `[Authorize(Roles = ...)]` cru é proibido — ver skill `autenticacao-e-autorizacao`.
- `[ProducesResponseType]` no que precisa aparecer no Swagger.
- **Atenção às rotas:** `UserController` tem `[Route("api/v1/[controller]")]` na classe mas todas
  as actions usam rota **absoluta** (`[HttpPost("/login")]`, `[HttpGet("/users/{id}")]`), então o
  prefixo da classe é ignorado. `ValidationController` usa `[Route("validation")]` com rotas
  relativas (`validation/verify`). Ao adicionar action, siga o estilo **do controller que está
  editando** — misturar gera rota surpresa. Rota nova é mudança de contrato:
  registre em `docs/FRONTEND_CHANGES.md`.

## Serviço de aplicação vs caso de uso

Existem os dois. Use caso de uso para **ação de negócio disparada por endpoint**; use
`I{Nome}Service` quando o comportamento é **consumido por outros casos de uso**
(`ITokenService`, `ISecureHashingService`, `IValidationService`). Serviço vai em
`UseCases/Interfaces/` + implementação em `UseCases/` ou `UseCases/Security/`.

Controller nunca injeta serviço quando existe caso de uso para a ação. A exceção atual é
`ValidationController`, que injeta `IValidationService` direto — desvio herdado, não padrão a
copiar.

## Entidade de domínio

```csharp
[CollectionName(nameof(User))]
public class User : BaseEntity
{
    public string Name { get; protected set; } = null!;
    public CreationInformation CreationInformations { get; protected set; } = null!;
    public ModificationInformation? ModificationInformations { get; protected set; }

    protected User() { }                                   // desserialização do Mongo
    private User(string name, string createdBy)             // construtor real
    {
        Name = name;
        CreationInformations = new CreationInformation(createdBy);
    }
    public static User Create(string name, string createdBy) => new(name, createdBy);

    public User ChangeName(string name, string modifiedBy)  // mutador nomeado, fluente
    {
        Name = name;
        ModificationInformations = new ModificationInformation(modifiedBy);
        return this;
    }
}
```

Checklist: herda `BaseEntity`; setters `protected`; construtor `protected` vazio; construtor
privado com parâmetros; factory `Create`; mutador de intenção retornando `this`;
`CreationInformation` no create e `ModificationInformation` em **cada** mutador;
`[CollectionName(nameof(Entidade))]`.

`CreationInformation`/`ModificationInformation` preenchem `CreatedAt`/`ModifiedAt` com
`DateTime.UtcNow` no construtor — sempre UTC, nunca `DateTime.Now`.

Contraexemplos no repositório (não copie): `Validation` tem setters públicos e é construída com
inicializador de objeto; `UserAssignment` recebe `createdBy`/`modifiedBy` e **não usa** —
não tem auditoria.

## Registro de DI

Tudo manual em `Program.cs`, sem Scrutor e sem auto-scan. Ao adicionar um caso de uso, adicione a
linha:

```csharp
builder.Services.AddScoped<CreateUserUseCase>();
```

Escopo por tipo: caso de uso e repositório `Scoped`; `IMongoClient`/`IMongoDbContext`/
`IAuthorizationHandler` `Singleton`. Esquecer de registrar dá erro só em runtime, na primeira
requisição — teste o endpoint de ponta a ponta ou pelo menos confira o `Program.cs` no PR.

`ServiceCollectionExtensions.AddShared()` existe mas **não é chamado**: MediatR, `IServiceFactory`
e `ServiceInstanceResolver<>` são código morto (DT-03). Não construa em cima deles.

## Serialização das respostas

`AddControllers().AddJsonOptions(x => x.JsonSerializerOptions.ReferenceHandler =
ReferenceHandler.Preserve)` — respostas HTTP saem com `$id`/`$ref`/`$values`. O cliente precisa
tratar. **Isso não afeta o SignalR**, que tem serializador próprio. Não mude o
`ReferenceHandler` sem registrar em `docs/FRONTEND_CHANGES.md`: é quebra de contrato para todo
consumidor REST.
