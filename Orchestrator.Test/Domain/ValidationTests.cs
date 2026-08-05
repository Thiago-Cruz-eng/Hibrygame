using Orchestrator.Domain;
using Xunit;

namespace Orchestrator.Test.Domain;

public class ValidationTests
{
    // ---------------------------------------------------------------
    // Property setters and defaults
    // ---------------------------------------------------------------

    [Fact]
    public void Validation_WhenCreated_IdIsNonEmptyGuid()
    {
        // Act
        var validation = new Validation();

        // Assert
        Assert.NotEqual(Guid.Empty, validation.Id);
    }

    [Fact]
    public void Validation_WhenCreated_DayOfGameDefaultsToApproximatelyNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var validation = new Validation();
        var after = DateTime.UtcNow;

        // Assert
        Assert.InRange(validation.DayOfGame, before, after);
    }

    [Fact]
    public void Validation_WhenCreated_DayOfGameIsUtc()
    {
        // Act
        var validation = new Validation();

        // Assert
        Assert.Equal(DateTimeKind.Utc, validation.DayOfGame.Kind);
    }

    [Fact]
    public void Validation_AcessToken_CanBeSetAndRead()
    {
        // Arrange
        var validation = new Validation();

        // Act
        validation.AcessToken = "my-access-token";

        // Assert
        Assert.Equal("my-access-token", validation.AcessToken);
    }

    [Fact]
    public void Validation_Room_NullableDefaultsToNull()
    {
        // Act
        var validation = new Validation();

        // Assert
        Assert.Null(validation.Room);
    }

    [Fact]
    public void Validation_Room_CanBeSetAndRead()
    {
        // Arrange
        var validation = new Validation();

        // Act
        validation.Room = "room-42";

        // Assert
        Assert.Equal("room-42", validation.Room);
    }

    [Fact]
    public void Validation_UserId_CanBeSetAndRead()
    {
        // Arrange
        var validation = new Validation();

        // Act
        validation.UserId = "user-123";

        // Assert
        Assert.Equal("user-123", validation.UserId);
    }

    [Fact]
    public void Validation_PieceColor_NullableDefaultsToNull()
    {
        // Act
        var validation = new Validation();

        // Assert
        Assert.Null(validation.PieceColor);
    }

    [Fact]
    public void Validation_PieceColor_CanBeSetAndRead()
    {
        // Arrange
        var validation = new Validation();

        // Act
        validation.PieceColor = "white";

        // Assert
        Assert.Equal("white", validation.PieceColor);
    }

    [Fact]
    public void Validation_UserEmail_CanBeSetAndRead()
    {
        // Arrange
        var validation = new Validation();

        // Act
        validation.UserEmail = "player@example.com";

        // Assert
        Assert.Equal("player@example.com", validation.UserEmail);
    }

    [Fact]
    public void Validation_DayOfGame_CanBeOverridden()
    {
        // Arrange
        var validation = new Validation();
        var customDate = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);

        // Act
        validation.DayOfGame = customDate;

        // Assert
        Assert.Equal(customDate, validation.DayOfGame);
    }
}
