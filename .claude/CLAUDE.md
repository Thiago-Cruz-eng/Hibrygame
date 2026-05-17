# verum-sales-global-backend

Plataforma B2B de vendas multi-país (US, MX — BR desabilitado via feature flag) da Cantu/Verum. API REST em ASP.NET Core 8 com Clean Architecture.

## Stack

- **Runtime:** .NET 8 / C# 12
- **Banco:** MongoDB Atlas (multi-database por país, sem migrations)
- **Cache:** Redis
- **Mensageria:** RabbitMQ (event bus)
- **Real-time:** Azure SignalR Service
- **Jobs:** Hangfire (persistido no MongoDB)
- **Auth:** JWT assinado via Azure Key Vault + Azure AD (opcional)
- **Logging/APM:** Serilog + Datadog
- **Deploy:** Docker + GitHub Actions → Azure Container Apps

## Como rodar localmente
~~~~
```bash
dotnet restore
dotnet build
dotnet run --project 2-Core/API
# Swagger: https://localhost:7237/swagger
```

Toda requisição precisa do header `X-Country: US` (ou `MX`). Sem ele, retorna 400 em modo strict.

## Estrutura de diretórios

```
0-Common/
  BrokerEvents/        # Definições de eventos RabbitMQ
  Contracts/           # DTOs e modelos compartilhados (214 arquivos)
  Security/            # Pacote JWT, Azure Key Vault, Azure AD
  Shared/              # BaseEntity, BaseRepository, Multitenancy, Interfaces

1-Modules/
  US/                  # Serviços e lógica específica dos EUA
  MX/                  # Serviços e lógica específica do México

2-Core/
  API/                 # Controllers, Middleware, Filters, Swagger, Program.cs
  Application/         # Services, Queries, Jobs (Hangfire), ConsumersBroker (RabbitMQ)
  Domain/              # Entidades, Enums, Constraints
  Infrastructure/      # Repositories MongoDB, Persistência

8-Automation/          # Scripts PowerShell para geração de CRUD
9-Adapters/            # SAP, Zendesk, Verum OMS, ClosedXML, SignalR, Redis, RabbitMQ, Email, PIX
```

## Arquitetura

Clean Architecture em fluxo unidirecional:

```
Domain → Application → Infrastructure → API
```

### Multi-tenância por país

Cada request carrega `X-Country` header. Isso determina:
- Qual banco MongoDB usar (`VerumSalesGlobal_US`, `VerumSalesGlobal_MX`, etc.)
- Qual implementação de service resolver (via `IServiceFactory`)
- Qual CORS policy e origens aceitar

### Factory pattern para resolução por país

```csharp
// No controller — nunca injetar IMyService diretamente
var service = factory.Create<IMyEntityService>();
```

`IServiceFactory.Create<T>()` tenta na ordem:
1. Service registrado com key do país (`"US"`, `"MX"`)
2. Service registrado com key `"Global"`
3. Service genérico (sem key)

### UserIdentification

Populado automaticamente pelo middleware de autenticação. Disponível via DI:

```csharp
[FromServices] UserIdentification userIdentification
```

Contém: `UserId`, `Name`, `Email`, `Country`, `Assignments` (empresa + role + hierarquia).

## Convenções de nomenclatura

| Tipo | Padrão | Exemplo |
|---|---|---|
| Controller | `{Entity}Controller` | `CustomerController` |
| Service interface | `I{Entity}Service` | `ICustomerService` |
| Service class | `{Entity}Service` | `CustomerService` |
| Repository interface | `I{Entity}Repository` | `ICustomerRepository` |
| Repository class | `{Entity}Repository` | `CustomerRepository` |
| DTO criação | `{Entity}CreateDto` | `CustomerCreateDto` |
| DTO atualização | `{Entity}UpdateDto` | `CustomerUpdateDto` |
| DTO resposta | `{Entity}Dto` | `CustomerDto` |
| Entidade domain | herda `AuditableEntity` | `Customer : AuditableEntity` |
| Validador | `{Entity}Validator` | `CustomerValidator` |
| Módulo país | `{Country}{Entity}Service` | `USCustomerService` |
| Marker de módulo | `{Country}ApplicationMarker` | `USApplicationMarker` |

## Padrões de código

### Entidade do Domain

```csharp
[BsonIgnoreExtraElements]
[CollectionName(nameof(MyEntity))]
public class MyEntity : AuditableEntity
{
    public string Name { get; protected set; }
    public string CompanyId { get; protected set; }

    protected MyEntity(string name, string companyId, string createdBy)
    {
        Name = name;
        CompanyId = companyId;
        CreationInformations = new CreationInformation(createdBy);
    }

    public static MyEntity Create(string name, string companyId, string createdBy) =>
        new(name, companyId, createdBy);

    public MyEntity ChangeName(string name, string modifiedBy)
    {
        Name = name;
        ModificationInformations = new ModificationInformation(modifiedBy);
        return this;
    }
}
```

- Constructor `protected`, instanciação via factory method `Create()`
- Setters `protected` — domain invariants ficam na entidade
- Métodos mutadores retornam `this` (fluent)
- `[CollectionName]` mapeia para a collection MongoDB

### Controller

```csharp
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class MyEntityController(IServiceFactory factory) : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(
        string id,
        [FromServices] UserIdentification userIdentification,
        CancellationToken cancellationToken)
    {
        var service = factory.Create<IMyEntityService>();
        var result = await service.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
```

- Sempre usar `IServiceFactory.Create<T>()` — nunca injetar o service diretamente
- `UserIdentification` sempre via `[FromServices]`
- `CancellationToken` em todos os endpoints async

### Service

```csharp
public class MyEntityService : BaseService, IMyEntityService
{
    private readonly IGenericRepository _repository;
    private readonly UserIdentification _userIdentification;

    public MyEntityService(
        IGenericRepository repository,
        UserIdentification userIdentification)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _userIdentification = userIdentification ?? throw new ArgumentNullException(nameof(userIdentification));
    }

    public async Task<MyEntityDto?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync<MyEntity>(id, cancellationToken);
        return entity?.MapToDto();
    }
}
```

- Herdar de `BaseService`
- Null checks explícitos no constructor
- Retornar `null` em caso de não encontrado — não lançar exceção
- Sempre `async/await` com `CancellationToken`

### Repository (para queries específicas)

```csharp
public class MyEntityRepository : BaseCountryRepository<MyEntity>, IMyEntityRepository
{
    public async Task<IEnumerable<MyEntity>> GetByCompanyIdAsync(
        string companyId,
        CancellationToken cancellationToken = default)
    {
        var collection = await GetCollectionAsync();
        return await collection
            .Find(x => x.CompanyId == companyId)
            .ToListAsync(cancellationToken);
    }
}
```

- `GetCollectionAsync()` já resolve o país via `X-Country` header
- Read: `GetCollectionAsync(isRead: true)` → `SecondaryPreferred`
- Write: `GetCollectionAsync(isRead: false)` → `PrimaryPreferred`
- Para queries simples, usar `IGenericRepository` direto no service

## Registro de dependências (DI)

Serviços em `Application/Services/*` são registrados automaticamente via Scrutor (scan por namespace + interface matching). Para registros manuais ou jobs:

```csharp
// em Application/Extensions/ServiceCollectionExtensions.cs
services.AddScoped<IMyEntityService, MyEntityService>();
services.AddScoped<MyEntityJob>();
```

Serviços específicos de país ficam nos módulos (`1-Modules/US/Extensions/ServicesRegister.cs`) e são registrados com key do país:

```csharp
services.AddKeyedScoped<IMyEntityService, USMyEntityService>("US");
```

## Autenticação e secrets

- Nunca colocar secrets em `appsettings.json` — usar referências ao Azure Key Vault
- Formato de referência: `"Azure--SecretName"` no appsettings é resolvido via `ISecretProvider`
- Para acessar secrets no código: `await _secretProvider.GetSecretAsync("External:ApiKey:US")`
- Em desenvolvimento: `FallbackSecretProvider` usa appsettings diretamente

## Geração automática de CRUD

Para novo domínio completo, usar o script PowerShell em `8-Automation/`:

```powershell
./Generate-Crud.ps1 -EntityName "MinhaEntidade" -Countries "US,MX"
```

Gera controller, DTOs, service, interface, repository, entidade, AutoMapper profile e registro DI.

## Integrações externas (9-Adapters)

| Adapter | Uso |
|---|---|
| `SapAdapter` | Pedidos, margens, crédito via SAP CPI |
| `ZendeskAdapter` | Criação de tickets de suporte |
| `VerumOMSAdapter` | Order Management System |
| `EmailAdapter` | Envio via SendGrid |
| `RabbitMQAdapter` | Publicação/consumo de eventos |
| `RedisCachingAdapter` | Cache distribuído |
| `SignalRAdapter` | Notificações real-time |
| `ClosedXmlAdapter` | Import/export Excel |
| `PixQrCodeAdapter` | QR Code PIX |

## Testes

Cobertura atual: apenas `0-Common/Security/Tests/` (3 testes unitários — JWT, Key Vault, MongoContext).

```bash
dotnet test
```

## Observações importantes

- **Feature flags por país:** `Features:EnableUS`, `Features:EnableMX`, `Features:EnableBR` em appsettings
- **Localização:** Suporte a `en-US`, `pt-BR`, `es-MX` via `ITextResourceProvider`
- **Auditoria:** Toda entidade que herda `AuditableEntity` rastreia criador/modificador automaticamente
- **SignalR:** JWT pode ser passado via query string para `/hubs/notifications` (WebSocket handshake)
- **Swagger:** Protegido por role `SwaggerReader-US` em produção
- **Read preference MongoDB:** reads vão para réplica secundária, writes para primária

## Documentação por Domínio

Cada arquivo documenta o fluxo completo de cada funcionalidade: Controller → Middleware → Service → Repository → Resposta.

| Arquivo | Domínio | Controllers cobertos |
|---------|---------|----------------------|
| [docs/00-fluxos-compartilhados.md](../docs/00-fluxos-compartilhados.md) | **Base/Infra** — middleware, JWT, IServiceFactory, IGenericRepository, AuditableEntity | Todos |
| [docs/01-autenticacao.md](../docs/01-autenticacao.md) | **Autenticação** — login, refresh token, reset de senha | `AuthController` |
| [docs/02-usuarios.md](../docs/02-usuarios.md) | **Usuários** — CRUD, hierarquia, importação em lote | `UserController`, `SalespersonController` |
| [docs/03-empresa-hierarquia.md](../docs/03-empresa-hierarquia.md) | **Empresa e Hierarquia** — empresas, nós hierárquicos, roles | `CompanyController`, `HierarchyNodeController`, `RoleController` |
| [docs/04-clientes.md](../docs/04-clientes.md) | **Clientes** — ciclo de vida, revisão, clustering, credenciais API | `CustomerController`, `ClusterController`, `CustomerAPICredentialController` |
| [docs/05-oportunidades.md](../docs/05-oportunidades.md) | **Oportunidades** — CRUD, detalhes por seção, motivos de decisão | `OpportunityController`, `OpportunityDetailController`, `OpportunityReasonsController` |
| [docs/06-catalogo.md](../docs/06-catalogo.md) | **Catálogo** — produtos, armazéns, estoque, expedição | `ProductController`, `WarehouseController`, `StockController` |
| [docs/07-precos.md](../docs/07-precos.md) | **Preços** — preço base, markups, exportação Excel | `ProductPriceController` |
| [docs/08-pagamento.md](../docs/08-pagamento.md) | **Pagamento** — formas e condições de pagamento | `PaymentMethodController`, `PaymentTermController` |
| [docs/09-logistica.md](../docs/09-logistica.md) | **Logística** — transportadoras, frete promocional | `CarrierController`, `FreightController` |
| [docs/10-tarefas.md](../docs/10-tarefas.md) | **Tarefas** — CRM tasks (call, visit, email), atribuição, status | `TaskController` |
| [docs/11-notificacoes.md](../docs/11-notificacoes.md) | **Notificações** — push SignalR, persistência, leitura | `NotificationsController` |
| [docs/12-configuracoes.md](../docs/12-configuracoes.md) | **Configurações** — estados, países, impostos MX, filtros de catálogo | `ConfigurationController`, `TaxesController`, `ReasonForExemptionController`, `FilterController` |
| [docs/13-dashboard-relatorios.md](../docs/13-dashboard-relatorios.md) | **Dashboard e Relatórios** — rankings, totais, exportação assíncrona Hangfire | `SalesDashboardController`, `ReportsController` |
| [docs/14-integracoes.md](../docs/14-integracoes.md) | **Integrações** — SAP (crédito, margem, pedido), Zendesk (clientes/tickets) | `SapIntegrationController`, `ZendeskController` |
| [docs/15-arquivos-templates.md](../docs/15-arquivos-templates.md) | **Arquivos e Templates** — upload/download, templates de e-mail | `FileController`, `EmailTemplateController` |
