using Orchestrator.Domain;
using Xunit;

namespace Orchestrator.Test.Domain;

public class AuditInformationTests
{
    // ---------------------------------------------------------------
    // CreationInformation
    // ---------------------------------------------------------------

    [Fact]
    public void CreationInformation_WhenCreated_SetsCreatedBy()
    {
        // Arrange / Act
        var info = new CreationInformation("system");

        // Assert
        Assert.Equal("system", info.CreatedBy);
    }

    [Fact]
    public void CreationInformation_WhenCreated_CreatedAtIsApproximatelyNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var info = new CreationInformation("system");
        var after = DateTime.UtcNow;

        // Assert
        Assert.InRange(info.CreatedAt, before, after);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("user-123")]
    [InlineData("service-account")]
    public void CreationInformation_WhenCreated_PreservesCreatedByExactly(string createdBy)
    {
        // Act
        var info = new CreationInformation(createdBy);

        // Assert
        Assert.Equal(createdBy, info.CreatedBy);
    }

    // ---------------------------------------------------------------
    // ModificationInformation
    // ---------------------------------------------------------------

    [Fact]
    public void ModificationInformation_WhenCreated_SetsModifiedBy()
    {
        // Arrange / Act
        var info = new ModificationInformation("editor");

        // Assert
        Assert.Equal("editor", info.ModifiedBy);
    }

    [Fact]
    public void ModificationInformation_WhenCreated_ModifiedAtIsApproximatelyNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var info = new ModificationInformation("editor");
        var after = DateTime.UtcNow;

        // Assert
        Assert.InRange(info.ModifiedAt, before, after);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("user-456")]
    [InlineData("batch-job")]
    public void ModificationInformation_WhenCreated_PreservesModifiedByExactly(string modifiedBy)
    {
        // Act
        var info = new ModificationInformation(modifiedBy);

        // Assert
        Assert.Equal(modifiedBy, info.ModifiedBy);
    }

    [Fact]
    public void ModificationInformation_TwoInstancesCreatedSequentially_ModifiedAtIsNotEarlierThanCreatedAt()
    {
        // Arrange
        var creation = new CreationInformation("admin");

        // Act
        var modification = new ModificationInformation("editor");

        // Assert
        Assert.True(modification.ModifiedAt >= creation.CreatedAt);
    }
}
