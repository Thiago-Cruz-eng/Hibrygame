using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Orchestrator.Domain;
using Orchestrator.Infra.Interfaces;
using Orchestrator.Presentation;
using Orchestrator.UseCases;
using Orchestrator.UseCases.Dto.Request;
using Orchestrator.UseCases.Dto.Response;
using Orchestrator.UseCases.Interfaces;
using Xunit;

namespace Orchestrator.Test.Presentation;

/// <summary>
/// Autorizacao dos dois caminhos de criacao de conta.
///
///   POST /register  anonimo, papel e autor decididos no servidor
///   POST /users     exige Role:Admin, aceita papel pelo corpo
///
/// A politica Role:Admin em si e do pipeline de autorizacao (coberta por
/// MinimumRoleHandlerTests). Aqui verifica-se o que o controller decide por conta propria:
/// nao criar papel acima do proprio nivel e derivar CreatedBy do token.
///
/// O UserController nao tinha nenhum teste — foi por isso que fechar o endpoint anonimo
/// nao quebrou nada na suite.
/// </summary>
public class UserControllerTests
{
    private static UserController BuildController(
        Mock<IUserRepositoryNoSql> userRepo,
        string? callerId,
        string? callerRole)
    {
        var hashing = new Mock<ISecureHashingService>();
        hashing.Setup(h => h.HashValue(It.IsAny<string>())).Returns(("hash", "salt"));

        var createUser = new CreateUserUseCase(
            userRepo.Object, hashing.Object, new Mock<ILogger<CreateUserUseCase>>().Object);

        var login = new LoginAsyncUseCase(
            new Mock<IValidationService>().Object,
            new Mock<ILogger<LoginAsyncUseCase>>().Object,
            userRepo.Object,
            new Mock<IRefreshTokenRepositoryNoSql>().Object,
            hashing.Object,
            new Mock<ITokenService>().Object);

        var register = new RegisterUserUseCase(
            createUser, login, new Mock<ILogger<RegisterUserUseCase>>().Object);

        // Os casos de uso que estes testes nao exercitam entram como null!: Register e
        // CreateUser sao os unicos caminhos aqui, e mockar classe concreta sem interface
        // exigiria casar assinatura de construtor sem ganho nenhum.
        var controller = new UserController(
            createUserUseCase: createUser,
            getUserUseCase: null!,
            loginAsync: login,
            updateUserUseCase: null!,
            deleteUserUseCase: null!,
            changePasswordUseCase: null!,
            refreshTokenUseCase: null!,
            registerUserUseCase: register);

        var claims = new List<Claim>();
        if (callerId is not null) claims.Add(new Claim(JwtRegisteredClaimNames.Sub, callerId));
        if (callerRole is not null) claims.Add(new Claim(ClaimTypes.Role, callerRole));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        return controller;
    }


    private static Mock<IUserRepositoryNoSql> EmptyRepo()
    {
        var repo = new Mock<IUserRepositoryNoSql>();
        repo.Setup(r => r.FindByFilter(
                It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<User>());
        repo.Setup(r => r.Save(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return repo;
    }

    private static CreateUserRequest AdminCreate(string role) => new()
    {
        Name = "Alvo",
        Email = $"alvo-{Guid.NewGuid()}@h.local",
        Password = "Senha!Forte123",
        PasswordConfirmation = "Senha!Forte123",
        Role = role,
        CreatedBy = "forjado-pelo-cliente"
    };

    // -----------------------------------------------------------------
    // POST /users — nao criar papel acima do proprio nivel
    // -----------------------------------------------------------------

    [Fact]
    public async Task CreateUser_WhenTheRequestedRoleIsAboveTheCallers_IsRejected()
    {
        // Um "adm" (nivel 4) nao cria um "super adm" (nivel 5). Antes o endpoint era
        // anonimo e qualquer um criava super adm.
        var repo = EmptyRepo();
        var controller = BuildController(repo, callerId: "adm-1", callerRole: "adm");

        var result = await controller.CreateUser(AdminCreate("super adm"));

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        var body = Assert.IsType<CreateUserResponse>(bad.Value);
        Assert.False(body.Success);
        Assert.Contains("above your own", body.Message);
        repo.Verify(r => r.Save(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateUser_WhenTheRequestedRoleIsAtOrBelowTheCallers_IsAllowed()
    {
        var repo = EmptyRepo();
        var controller = BuildController(repo, callerId: "adm-1", callerRole: "adm");

        var result = await controller.CreateUser(AdminCreate("jogador"));

        Assert.IsType<OkObjectResult>(result);
        repo.Verify(r => r.Save(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateUser_DerivesCreatedByFromTheToken_NotFromTheBody()
    {
        // CreatedBy vinha do corpo, entao a auditoria de criacao era forjavel.
        var repo = EmptyRepo();
        User? saved = null;
        repo.Setup(r => r.Save(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => saved = u)
            .Returns(Task.CompletedTask);

        var controller = BuildController(repo, callerId: "adm-42", callerRole: "adm");

        await controller.CreateUser(AdminCreate("jogador"));

        Assert.NotNull(saved);
        Assert.Equal("adm-42", saved!.CreationInformations.CreatedBy);
        Assert.NotEqual("forjado-pelo-cliente", saved.CreationInformations.CreatedBy);
    }

    [Fact]
    public async Task CreateUser_WithoutASubClaim_IsForbidden()
    {
        var controller = BuildController(EmptyRepo(), callerId: null, callerRole: "adm");

        Assert.IsType<ForbidResult>(await controller.CreateUser(AdminCreate("jogador")));
    }

    [Fact]
    public async Task CreateUser_WithoutARoleClaim_IsForbidden()
    {
        var controller = BuildController(EmptyRepo(), callerId: "adm-1", callerRole: null);

        Assert.IsType<ForbidResult>(await controller.CreateUser(AdminCreate("jogador")));
    }

    [Fact]
    public async Task CreateUser_WithAnUnknownRole_IsRejected()
    {
        var controller = BuildController(EmptyRepo(), callerId: "adm-1", callerRole: "adm");

        var result = await controller.CreateUser(AdminCreate("imperador"));

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid role", Assert.IsType<CreateUserResponse>(bad.Value).Message);
    }

    // -----------------------------------------------------------------
    // POST /register — o servidor decide papel e autor
    // -----------------------------------------------------------------

    [Fact]
    public async Task Register_AlwaysCreatesAPlayer_RegardlessOfWhatTheClientCouldWant()
    {
        // RegisterRequest nao tem campo Role: nao ha como pedir outro papel.
        var repo = EmptyRepo();
        User? saved = null;
        repo.Setup(r => r.Save(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => saved = u)
            .Returns(Task.CompletedTask);

        var controller = BuildController(repo, callerId: null, callerRole: null);

        await controller.Register(new RegisterRequest
        {
            Name = "Jogadora",
            Email = "jogadora@h.local",
            Password = "Senha!Forte123",
            PasswordConfirmation = "Senha!Forte123"
        });

        Assert.NotNull(saved);
        Assert.Equal("jogador", saved!.Role);
        Assert.Equal("self-registration", saved.CreationInformations.CreatedBy);
    }

    [Fact]
    public async Task Register_NormalizesTheEmail()
    {
        var repo = EmptyRepo();
        User? saved = null;
        repo.Setup(r => r.Save(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => saved = u)
            .Returns(Task.CompletedTask);

        var controller = BuildController(repo, null, null);

        await controller.Register(new RegisterRequest
        {
            Name = "  Espacos  ",
            Email = "  MAIUSCULA@H.LOCAL ",
            Password = "Senha!Forte123",
            PasswordConfirmation = "Senha!Forte123"
        });

        Assert.Equal("maiuscula@h.local", saved!.Email);
        Assert.Equal("Espacos", saved.Name);
    }

    [Fact]
    public async Task Register_WhenTheEmailIsTaken_IsRejected()
    {
        var repo = new Mock<IUserRepositoryNoSql>();
        repo.Setup(r => r.FindByFilter(
                It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                User.Create("Existente", "tomado@h.local", "jogador", "h", "s", [], "seed")
            });

        var controller = BuildController(repo, null, null);

        var result = await controller.Register(new RegisterRequest
        {
            Name = "Nova",
            Email = "tomado@h.local",
            Password = "Senha!Forte123",
            PasswordConfirmation = "Senha!Forte123"
        });

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.False(Assert.IsType<RegisterResponse>(bad.Value).Success);
        repo.Verify(r => r.Save(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
