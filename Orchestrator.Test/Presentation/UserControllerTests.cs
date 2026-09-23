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

    // -----------------------------------------------------------------
    // Autorizacao dos endpoints que operam sobre UM usuario
    //
    // GET /users/{id}, PUT /users/{id}, DELETE /users/{id} e
    // POST /users/change-password agiam sobre um usuario identificado pelo CORPO ou pela
    // ROTA, sem comparar com o token. Era a DT-16.
    // -----------------------------------------------------------------

    /// <summary>
    /// Um controller com os casos de uso de leitura, alteracao, remocao e troca de senha
    /// realmente montados — o <see cref="BuildController"/> acima passa null! neles porque os
    /// testes de cadastro nao os exercitam.
    /// </summary>
    private static (UserController Controller, Mock<IUserRepositoryNoSql> Repo) BuildFullController(
        string? callerId,
        string? callerRole,
        User? stored = null)
    {
        var repo = new Mock<IUserRepositoryNoSql>();
        var users = stored is null ? Array.Empty<User>() : new[] { stored };

        // O dublê não aplica o filtro, então a primeira consulta devolve o alvo e as seguintes
        // devolvem vazio. Sem isso, a checagem de e-mail duplicado de UpdateUserUseCase
        // encontraria o próprio usuário e recusaria toda alteração com "Email already in use".
        var lookups = 0;
        repo.Setup(r => r.FindByFilter(
                It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++lookups == 1 ? users : Array.Empty<User>());
        repo.Setup(r => r.GetById(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(stored);
        repo.Setup(r => r.Update(It.IsAny<string>(), It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        repo.Setup(r => r.Delete(It.IsAny<string>(), It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        repo.Setup(r => r.UpdatePassword(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var refreshTokens = new Mock<IRefreshTokenRepositoryNoSql>();
        refreshTokens.Setup(r => r.FindByFilter(
                It.IsAny<System.Linq.Expressions.Expression<Func<RefreshToken, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RefreshToken>());

        var validations = new Mock<IValidationRepositoryNoSql>();
        validations.Setup(r => r.FindByFilter(
                It.IsAny<System.Linq.Expressions.Expression<Func<Validation, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Validation>());

        var hashing = new Mock<ISecureHashingService>();
        hashing.Setup(h => h.HashValue(It.IsAny<string>())).Returns(("hash", "salt"));
        hashing.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var controller = new UserController(
            createUserUseCase: null!,
            getUserUseCase: new GetUserUseCase(repo.Object, new Mock<ILogger<GetUserUseCase>>().Object),
            loginAsync: null!,
            updateUserUseCase: new UpdateUserUseCase(
                repo.Object, new Mock<ILogger<UpdateUserUseCase>>().Object),
            deleteUserUseCase: new DeleteUserUseCase(
                repo.Object, refreshTokens.Object, validations.Object,
                new Mock<ILogger<DeleteUserUseCase>>().Object),
            changePasswordUseCase: new ChangePasswordUseCase(
                repo.Object, refreshTokens.Object, hashing.Object,
                new Mock<ILogger<ChangePasswordUseCase>>().Object),
            refreshTokenUseCase: null!,
            registerUserUseCase: null!);

        var claims = new List<Claim>();
        if (callerId is not null) claims.Add(new Claim(JwtRegisteredClaimNames.Sub, callerId));
        if (callerRole is not null) claims.Add(new Claim(ClaimTypes.Role, callerRole));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"))
            }
        };

        return (controller, repo);
    }

    private static User StoredUser(string role = "jogador")
        => User.Create("Alvo", "alvo@h.local", role, "hash", "salt", [], "seed");

    private static UpdateUserRequest UpdateBody(string role = "jogador") => new()
    {
        Name = "Nome Novo",
        Email = "novo@h.local",
        Role = role,
        ModifiedBy = "forjado-pelo-cliente",
        Assignments = []
    };

    // --- GET /users/{id} ---

    [Fact]
    public async Task GetUser_ReadingYourOwnAccount_IsAllowed()
    {
        var target = StoredUser();
        var (controller, _) = BuildFullController(target.Id.ToString(), "jogador", target);

        var result = await controller.GetUser(target.Id.ToString());

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetUser_ReadingSomeoneElseAsAPlayer_Returns404()
    {
        // 404 e nao 403 de proposito: 403 confirmaria que aquele id existe, e confirmar
        // existencia e metade do trabalho de quem esta sondando.
        var target = StoredUser();
        var (controller, repo) = BuildFullController("outro-usuario", "jogador", target);

        var result = await controller.GetUser(target.Id.ToString());

        Assert.IsType<NotFoundResult>(result);

        // E nem chega a consultar: a recusa acontece antes do caso de uso.
        repo.Verify(r => r.FindByFilter(
                It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetUser_ReadingSomeoneElseAsAnAdmin_IsAllowed()
    {
        var target = StoredUser();
        var (controller, _) = BuildFullController("adm-1", "adm", target);

        var result = await controller.GetUser(target.Id.ToString());

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetUser_WithoutASubClaim_Returns404()
    {
        var target = StoredUser();
        var (controller, _) = BuildFullController(callerId: null, callerRole: "adm", stored: target);

        Assert.IsType<NotFoundResult>(await controller.GetUser(target.Id.ToString()));
    }

    // --- PUT /users/{id} ---

    [Fact]
    public async Task UpdateUser_WithoutASubClaim_IsForbidden()
    {
        var target = StoredUser();
        var (controller, _) = BuildFullController(null, "adm", target);

        Assert.IsType<ForbidResult>(await controller.UpdateUser(target.Id.ToString(), UpdateBody()));
    }

    [Fact]
    public async Task UpdateUser_WithoutARoleClaim_IsForbidden()
    {
        var target = StoredUser();
        var (controller, _) = BuildFullController("adm-1", null, target);

        Assert.IsType<ForbidResult>(await controller.UpdateUser(target.Id.ToString(), UpdateBody()));
    }

    [Fact]
    public async Task UpdateUser_PassesTheCallersLevelToTheUseCase()
    {
        // O "lider de time" nao pode promover ninguem a "super adm". A regra mora no caso de uso;
        // o que se verifica aqui e que o controller entrega o nivel certo.
        var target = StoredUser();
        var (controller, _) = BuildFullController("lider-1", "lider de time", target);

        var result = await controller.UpdateUser(target.Id.ToString(), UpdateBody("super adm"));

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var body = Assert.IsType<UpdateUserResponse>(notFound.Value);
        Assert.Equal("Cannot assign a role above your own.", body.Message);
    }

    [Fact]
    public async Task UpdateUser_AuditsTheCallerFromTheToken()
    {
        var target = StoredUser();
        var (controller, _) = BuildFullController("adm-99", "adm", target);

        await controller.UpdateUser(target.Id.ToString(), UpdateBody());

        Assert.Equal("adm-99", target.ModificationInformations!.ModifiedBy);
    }

    // --- DELETE /users/{id} ---

    [Fact]
    public async Task DeleteUser_WithoutASubClaim_IsForbidden()
    {
        var target = StoredUser();
        var (controller, _) = BuildFullController(null, "adm", target);

        Assert.IsType<ForbidResult>(await controller.DeleteUser(target.Id.ToString()));
    }

    [Fact]
    public async Task DeleteUser_PassesTheCallersLevelToTheUseCase()
    {
        var target = StoredUser("super adm");
        var (controller, repo) = BuildFullController("adm-1", "adm", target);

        var result = await controller.DeleteUser(target.Id.ToString());

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var body = Assert.IsType<DeleteUserResponse>(notFound.Value);
        Assert.Equal("Cannot delete a user with a role above your own.", body.Message);
        repo.Verify(
            r => r.Delete(It.IsAny<string>(), It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- POST /users/change-password ---

    [Fact]
    public async Task ChangePassword_TargetsTheCaller_NotTheUserIdInTheBody()
    {
        // O corpo pede a senha de OUTRA pessoa; o servidor troca a de quem chamou. Era por aqui
        // que qualquer jogador autenticado trocava a senha de qualquer usuario.
        var caller = StoredUser();
        var (controller, repo) = BuildFullController(caller.Id.ToString(), "jogador", caller);

        var result = await controller.ChangePassword(new ChangePasswordRequest
        {
            UserId = Guid.NewGuid().ToString(),
            ModifiedBy = "forjado",
            CurrentPassword = "SenhaAtual1",
            NewPassword = "SenhaNova123",
            NewPasswordConfirmation = "SenhaNova123"
        });

        Assert.IsType<OkObjectResult>(result);
        repo.Verify(r => r.GetById(caller.Id, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(caller.Id.ToString(), caller.ModificationInformations!.ModifiedBy);
    }

    [Fact]
    public async Task ChangePassword_WithoutASubClaim_IsForbidden()
    {
        var (controller, _) = BuildFullController(null, "jogador");

        var result = await controller.ChangePassword(new ChangePasswordRequest
        {
            UserId = Guid.NewGuid().ToString(),
            CurrentPassword = "SenhaAtual1",
            NewPassword = "SenhaNova123",
            NewPasswordConfirmation = "SenhaNova123",
            ModifiedBy = "x"
        });

        Assert.IsType<ForbidResult>(result);
    }
}
