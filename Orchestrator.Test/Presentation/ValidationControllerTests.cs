using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Orchestrator.Domain;
using Orchestrator.Presentation;
using Orchestrator.UseCases.Dto.Request;
using Orchestrator.UseCases.Dto.Response;
using Orchestrator.UseCases.Interfaces;
using Xunit;

namespace Orchestrator.Test.Presentation;

public class ValidationControllerTests
{
    // ---------------------------------------------------------------
    // Builder helper
    // ---------------------------------------------------------------

    /// <summary>
    /// Builds a ValidationController whose HttpContext is wired with:
    ///   - a ClaimsPrincipal carrying <paramref name="subClaim"/> (or no sub claim when null)
    ///   - an IAuthenticationService that returns an AuthenticateResult whose
    ///     AuthenticationProperties holds the <paramref name="accessToken"/> (or none when null)
    /// </summary>
    private static (ValidationController controller, Mock<IValidationService> svc) BuildController(
        string? subClaim,
        string? accessToken)
    {
        var svc = new Mock<IValidationService>();
        var controller = new ValidationController(svc.Object);

        // Build ClaimsPrincipal
        var claims = new List<Claim>();
        if (subClaim is not null)
            claims.Add(new Claim(JwtRegisteredClaimNames.Sub, subClaim));
        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        // Build AuthenticationProperties with or without the access_token
        var props = new AuthenticationProperties();
        if (accessToken is not null)
            props.StoreTokens(new[] { new AuthenticationToken { Name = "access_token", Value = accessToken } });

        var authTicket = new AuthenticationTicket(principal, props, "Bearer");

        // Mock IAuthenticationService — this is what GetTokenAsync calls internally
        var authServiceMock = new Mock<IAuthenticationService>();
        authServiceMock
            .Setup(x => x.AuthenticateAsync(It.IsAny<HttpContext>(), It.IsAny<string?>()))
            .ReturnsAsync(AuthenticateResult.Success(authTicket));

        var services = new ServiceCollection();
        services.AddSingleton(authServiceMock.Object);
        var sp = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            User = principal,
            RequestServices = sp
        };

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return (controller, svc);
    }

    private static Validation BuildValidation(
        string userId = "user-1",
        string userEmail = "user@example.com",
        string? room = "room-1",
        string? pieceColor = "White")
        => new()
        {
            UserId = userId,
            UserEmail = userEmail,
            Room = room,
            PieceColor = pieceColor,
            AcessToken = "raw-jwt"
        };

    // ---------------------------------------------------------------
    // Verify — happy path
    // ---------------------------------------------------------------

    [Fact]
    public async Task Verify_HappyPath_ValidationExists_ReturnsOkWithValidTrue()
    {
        // Arrange
        var (controller, svc) = BuildController(subClaim: "user-1", accessToken: "raw-jwt");
        var validation = BuildValidation(userId: "user-1");
        svc.Setup(s => s.GetValidationByUserToken("user-1", "raw-jwt"))
           .ReturnsAsync(validation);

        // Act
        var result = await controller.Verify(new VerifyValidationRequest { UserId = "user-1" });

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<VerifyValidationResponse>(ok.Value);
        Assert.True(body.Valid);
    }

    [Fact]
    public async Task Verify_HappyPath_ValidationNull_ReturnsOkWithValidFalse()
    {
        // Arrange
        var (controller, svc) = BuildController(subClaim: "user-1", accessToken: "raw-jwt");
        svc.Setup(s => s.GetValidationByUserToken("user-1", "raw-jwt"))
           .ReturnsAsync((Validation)null!);

        // Act
        var result = await controller.Verify(new VerifyValidationRequest { UserId = "user-1" });

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<VerifyValidationResponse>(ok.Value);
        Assert.False(body.Valid);
    }

    // ---------------------------------------------------------------
    // Verify — sub mismatch → Forbid, no service call
    // ---------------------------------------------------------------

    [Fact]
    public async Task Verify_SubMismatch_ReturnsForbidAndDoesNotCallService()
    {
        // Arrange
        var (controller, svc) = BuildController(subClaim: "other-user", accessToken: "raw-jwt");

        // Act
        var result = await controller.Verify(new VerifyValidationRequest { UserId = "user-1" });

        // Assert
        Assert.IsType<ForbidResult>(result);
        svc.Verify(s => s.GetValidationByUserToken(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // ---------------------------------------------------------------
    // Verify — missing sub claim → Forbid
    // ---------------------------------------------------------------

    [Fact]
    public async Task Verify_MissingSubClaim_ReturnsForbidAndDoesNotCallService()
    {
        // Arrange
        var (controller, svc) = BuildController(subClaim: null, accessToken: "raw-jwt");

        // Act
        var result = await controller.Verify(new VerifyValidationRequest { UserId = "user-1" });

        // Assert
        Assert.IsType<ForbidResult>(result);
        svc.Verify(s => s.GetValidationByUserToken(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // ---------------------------------------------------------------
    // Verify — missing access_token → Forbid
    // ---------------------------------------------------------------

    [Fact]
    public async Task Verify_MissingAccessToken_ReturnsForbidAndDoesNotCallService()
    {
        // Arrange
        var (controller, svc) = BuildController(subClaim: "user-1", accessToken: null);

        // Act
        var result = await controller.Verify(new VerifyValidationRequest { UserId = "user-1" });

        // Assert
        Assert.IsType<ForbidResult>(result);
        svc.Verify(s => s.GetValidationByUserToken(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // ---------------------------------------------------------------
    // Get — happy path
    // ---------------------------------------------------------------

    [Fact]
    public async Task Get_HappyPath_ValidationExists_ReturnsOkWithMappedFields()
    {
        // Arrange
        var (controller, svc) = BuildController(subClaim: "user-1", accessToken: "raw-jwt");
        var validation = BuildValidation(userId: "user-1", userEmail: "user@example.com", room: "room-42", pieceColor: "Black");
        svc.Setup(s => s.GetValidationByUserToken("user-1", "raw-jwt"))
           .ReturnsAsync(validation);

        // Act
        var result = await controller.Get(new GetValidationRequest { UserId = "user-1" });

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<GetValidationResponse>(ok.Value);
        Assert.Equal(validation.Id.ToString(), body.Id);
        Assert.Equal("user-1", body.UserId);
        Assert.Equal("user@example.com", body.UserEmail);
        Assert.Equal("room-42", body.Room);
        Assert.Equal("Black", body.PieceColor);
    }

    // ---------------------------------------------------------------
    // Get — validation not found → NotFound
    // ---------------------------------------------------------------

    [Fact]
    public async Task Get_ValidationNotFound_ReturnsNotFound()
    {
        // Arrange
        var (controller, svc) = BuildController(subClaim: "user-1", accessToken: "raw-jwt");
        svc.Setup(s => s.GetValidationByUserToken("user-1", "raw-jwt"))
           .ReturnsAsync((Validation)null!);

        // Act
        var result = await controller.Get(new GetValidationRequest { UserId = "user-1" });

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    // ---------------------------------------------------------------
    // Get — sub mismatch → Forbid
    // ---------------------------------------------------------------

    [Fact]
    public async Task Get_SubMismatch_ReturnsForbidAndDoesNotCallService()
    {
        // Arrange
        var (controller, svc) = BuildController(subClaim: "other-user", accessToken: "raw-jwt");

        // Act
        var result = await controller.Get(new GetValidationRequest { UserId = "user-1" });

        // Assert
        Assert.IsType<ForbidResult>(result);
        svc.Verify(s => s.GetValidationByUserToken(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // ---------------------------------------------------------------
    // Get — missing sub claim → Forbid
    // ---------------------------------------------------------------

    [Fact]
    public async Task Get_MissingSubClaim_ReturnsForbidAndDoesNotCallService()
    {
        // Arrange
        var (controller, svc) = BuildController(subClaim: null, accessToken: "raw-jwt");

        // Act
        var result = await controller.Get(new GetValidationRequest { UserId = "user-1" });

        // Assert
        Assert.IsType<ForbidResult>(result);
        svc.Verify(s => s.GetValidationByUserToken(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // ---------------------------------------------------------------
    // Get — missing access_token → Forbid
    // ---------------------------------------------------------------

    [Fact]
    public async Task Get_MissingAccessToken_ReturnsForbidAndDoesNotCallService()
    {
        // Arrange
        var (controller, svc) = BuildController(subClaim: "user-1", accessToken: null);

        // Act
        var result = await controller.Get(new GetValidationRequest { UserId = "user-1" });

        // Assert
        Assert.IsType<ForbidResult>(result);
        svc.Verify(s => s.GetValidationByUserToken(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // ---------------------------------------------------------------
    // Update — happy path, service returns true
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_HappyPath_ServiceReturnsTrue_ReturnsOkWithUpdatedTrue()
    {
        // Arrange
        var (controller, svc) = BuildController(subClaim: "user-1", accessToken: "raw-jwt");
        svc.Setup(s => s.UpdateValidationByUserToken("user-1", "raw-jwt", "White", "room-1"))
           .ReturnsAsync(true);

        var req = new UpdateValidationRequest
        {
            UserId = "user-1",
            Room = "room-1",
            PieceColor = "White",
            UserEmail = "user@example.com"
        };

        // Act
        var result = await controller.Update("some-id", req);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<UpdateValidationResponse>(ok.Value);
        Assert.True(body.Updated);
    }

    // ---------------------------------------------------------------
    // Update — service returns false
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_HappyPath_ServiceReturnsFalse_ReturnsOkWithUpdatedFalse()
    {
        // Arrange
        var (controller, svc) = BuildController(subClaim: "user-1", accessToken: "raw-jwt");
        svc.Setup(s => s.UpdateValidationByUserToken("user-1", "raw-jwt", "White", "room-1"))
           .ReturnsAsync(false);

        var req = new UpdateValidationRequest
        {
            UserId = "user-1",
            Room = "room-1",
            PieceColor = "White",
            UserEmail = "user@example.com"
        };

        // Act
        var result = await controller.Update("some-id", req);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<UpdateValidationResponse>(ok.Value);
        Assert.False(body.Updated);
    }

    // ---------------------------------------------------------------
    // Update — sub mismatch → Forbid
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_SubMismatch_ReturnsForbidAndDoesNotCallService()
    {
        // Arrange
        var (controller, svc) = BuildController(subClaim: "other-user", accessToken: "raw-jwt");

        var req = new UpdateValidationRequest
        {
            UserId = "user-1",
            Room = "room-1",
            PieceColor = "White",
            UserEmail = "user@example.com"
        };

        // Act
        var result = await controller.Update("some-id", req);

        // Assert
        Assert.IsType<ForbidResult>(result);
        svc.Verify(s => s.UpdateValidationByUserToken(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // ---------------------------------------------------------------
    // Update — missing sub claim → Forbid
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_MissingSubClaim_ReturnsForbidAndDoesNotCallService()
    {
        // Arrange
        var (controller, svc) = BuildController(subClaim: null, accessToken: "raw-jwt");

        var req = new UpdateValidationRequest
        {
            UserId = "user-1",
            Room = "room-1",
            PieceColor = "White",
            UserEmail = "user@example.com"
        };

        // Act
        var result = await controller.Update("some-id", req);

        // Assert
        Assert.IsType<ForbidResult>(result);
        svc.Verify(s => s.UpdateValidationByUserToken(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // ---------------------------------------------------------------
    // Update — missing access_token → Forbid
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_MissingAccessToken_ReturnsForbidAndDoesNotCallService()
    {
        // Arrange
        var (controller, svc) = BuildController(subClaim: "user-1", accessToken: null);

        var req = new UpdateValidationRequest
        {
            UserId = "user-1",
            Room = "room-1",
            PieceColor = "White",
            UserEmail = "user@example.com"
        };

        // Act
        var result = await controller.Update("some-id", req);

        // Assert
        Assert.IsType<ForbidResult>(result);
        svc.Verify(s => s.UpdateValidationByUserToken(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // ---------------------------------------------------------------
    // CanMove — happy path, service returns true
    // ---------------------------------------------------------------

    [Fact]
    public async Task CanMove_HappyPath_ServiceReturnsTrue_ReturnsOkWithCanMoveTrue()
    {
        // Arrange
        var (controller, svc) = BuildController(subClaim: "user-1", accessToken: "raw-jwt");
        svc.Setup(s => s.GetValidationCanMove("user-1", "raw-jwt", "White", "room-1", "user@example.com", "2026-01-01"))
           .ReturnsAsync(true);

        var req = new CanMoveValidationRequest
        {
            UserId = "user-1",
            Room = "room-1",
            PieceColor = "White",
            UserEmail = "user@example.com",
            Day = "2026-01-01"
        };

        // Act
        var result = await controller.CanMove(req);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<CanMoveValidationResponse>(ok.Value);
        Assert.True(body.CanMove);
    }

    // ---------------------------------------------------------------
    // CanMove — service returns false
    // ---------------------------------------------------------------

    [Fact]
    public async Task CanMove_HappyPath_ServiceReturnsFalse_ReturnsOkWithCanMoveFalse()
    {
        // Arrange
        var (controller, svc) = BuildController(subClaim: "user-1", accessToken: "raw-jwt");
        svc.Setup(s => s.GetValidationCanMove("user-1", "raw-jwt", "White", "room-1", "user@example.com", "2026-01-01"))
           .ReturnsAsync(false);

        var req = new CanMoveValidationRequest
        {
            UserId = "user-1",
            Room = "room-1",
            PieceColor = "White",
            UserEmail = "user@example.com",
            Day = "2026-01-01"
        };

        // Act
        var result = await controller.CanMove(req);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<CanMoveValidationResponse>(ok.Value);
        Assert.False(body.CanMove);
    }

    // ---------------------------------------------------------------
    // CanMove — sub mismatch → Forbid
    // ---------------------------------------------------------------

    [Fact]
    public async Task CanMove_SubMismatch_ReturnsForbidAndDoesNotCallService()
    {
        // Arrange
        var (controller, svc) = BuildController(subClaim: "other-user", accessToken: "raw-jwt");

        var req = new CanMoveValidationRequest
        {
            UserId = "user-1",
            Room = "room-1",
            PieceColor = "White",
            UserEmail = "user@example.com",
            Day = "2026-01-01"
        };

        // Act
        var result = await controller.CanMove(req);

        // Assert
        Assert.IsType<ForbidResult>(result);
        svc.Verify(s => s.GetValidationCanMove(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // ---------------------------------------------------------------
    // CanMove — missing sub claim → Forbid
    // ---------------------------------------------------------------

    [Fact]
    public async Task CanMove_MissingSubClaim_ReturnsForbidAndDoesNotCallService()
    {
        // Arrange
        var (controller, svc) = BuildController(subClaim: null, accessToken: "raw-jwt");

        var req = new CanMoveValidationRequest
        {
            UserId = "user-1",
            Room = "room-1",
            PieceColor = "White",
            UserEmail = "user@example.com",
            Day = "2026-01-01"
        };

        // Act
        var result = await controller.CanMove(req);

        // Assert
        Assert.IsType<ForbidResult>(result);
        svc.Verify(s => s.GetValidationCanMove(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // ---------------------------------------------------------------
    // CanMove — missing access_token → Forbid
    // ---------------------------------------------------------------

    [Fact]
    public async Task CanMove_MissingAccessToken_ReturnsForbidAndDoesNotCallService()
    {
        // Arrange
        var (controller, svc) = BuildController(subClaim: "user-1", accessToken: null);

        var req = new CanMoveValidationRequest
        {
            UserId = "user-1",
            Room = "room-1",
            PieceColor = "White",
            UserEmail = "user@example.com",
            Day = "2026-01-01"
        };

        // Act
        var result = await controller.CanMove(req);

        // Assert
        Assert.IsType<ForbidResult>(result);
        svc.Verify(s => s.GetValidationCanMove(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
