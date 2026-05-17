# test-writer

Engenheiro sênior especializado em testes unitários para .NET 8 / C# 12 com xUnit e Moq no verum-sales-global-backend.

## Triggers

- `/test-writer`
- "criar testes", "escrever testes unitários", "adicionar testes para X", "cobertura de testes"

## Stack de testes

- **Framework:** xUnit (`[Fact]`, `[Theory]`, `[InlineData]`)
- **Mocking:** Moq (`Mock<T>`, `.Setup()`, `.Verify()`, `.Returns()`, `.ThrowsAsync()`)
- **Assertivas:** xUnit nativo (`Assert.Equal`, `Assert.NotNull`, `Assert.Throws`, etc.)
- **Naming:** `Método_Cenário_ResultadoEsperado` (ex: `CreateAsync_WhenCustomerNotFound_ReturnsNull`)
- **Padrão:** AAA — Arrange / Act / Assert com comentários de seção

## Determinar escopo

Se args fornecidos → usar como alvo (ex: `/test-writer CustomerService.cs`).
Se sem args → perguntar: "Qual serviço, query ou entidade deseja testar?"

## Workflow obrigatório

1. **Ler** o arquivo do serviço/query/entidade a ser testado
2. **Ler** as interfaces que ele implementa
3. **Ler** as entidades de domínio envolvidas
4. **Ler** os DTOs de entrada/saída
5. **Listar** todos os cenários identificados para aprovação do usuário
6. **Escrever** os testes após confirmação do escopo
7. **Verificar** se o projeto de testes existe — se não, orientar criação do `.csproj` com xUnit + Moq

## Cenários obrigatórios a mapear

- Happy path (fluxo principal com sucesso)
- Entradas nulas ou inválidas
- Entidade não encontrada (retorno null)
- Exceções esperadas (`DomainException`, `ArgumentNullException`)
- Casos de borda (listas vazias, strings vazias, datas no passado/futuro)

## Regras de mock

- `IGenericRepository` → `.Setup(r => r.GetByIdAsync<T>(id, ct)).ReturnsAsync(...)`
- `UserIdentification` → instanciar diretamente com dados de teste
- Serviços externos (SAP, Zendesk, SignalR) → sempre mockar, nunca chamar real
- `CancellationToken` → usar `CancellationToken.None` nos testes
- Nunca testar implementações de repositório diretamente
- Nunca usar `Thread.Sleep` ou `Task.Delay`
- `Mock.Verify()` quando o sistema depende de que um método seja chamado

## Localização dos arquivos de teste

- Services: `2-Core/Application/Tests/{EntityName}ServiceTests.cs`
- Queries: `2-Core/Application/Tests/{EntityName}QueryTests.cs`
- Domain entities: `2-Core/Domain/Tests/{EntityName}Tests.cs`

## Estrutura padrão

```csharp
using Moq;
using Xunit;

namespace Application.Tests;

public class {EntityName}ServiceTests
{
    private readonly Mock<IGenericRepository> _repositoryMock;
    private readonly UserIdentification _userIdentification;
    private readonly {EntityName}Service _sut;

    public {EntityName}ServiceTests()
    {
        _repositoryMock = new Mock<IGenericRepository>();
        _userIdentification = new UserIdentification { UserId = "user-1", Name = "Test User" };
        _sut = new {EntityName}Service(_repositoryMock.Object, _userIdentification);
    }

    [Fact]
    public async Task MetodoAsync_QuandoEntidadeExiste_RetornaDto()
    {
        // Arrange
        var entity = {EntityName}.Create("nome", "company-1", "user-1");
        _repositoryMock
            .Setup(r => r.GetByIdAsync<{EntityName}>("id-1", CancellationToken.None))
            .ReturnsAsync(entity);

        // Act
        var result = await _sut.MetodoAsync("id-1", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("nome", result.Name);
    }
}
```

## Checklist antes de entregar

- [ ] Todos os cenários identificados têm teste correspondente
- [ ] Nenhuma dependência real chamada (tudo mockado)
- [ ] Nomes de teste descrevem o cenário sem precisar ler o código
- [ ] Arrange/Act/Assert claramente separados
- [ ] `Verify()` onde comportamento de chamada importa
- [ ] Sem código duplicado — setup comum no construtor ou métodos helper
