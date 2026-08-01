using Orchestrator.Domain;
using Xunit;

namespace Orchestrator.Test.Domain;

public class BaseEntityTests
{
    // A concrete subclass for testing because BaseEntity is not abstract
    private class TestEntity : BaseEntity { }

    // ---------------------------------------------------------------
    // Id defaults
    // ---------------------------------------------------------------

    [Fact]
    public void Id_WhenEntityCreated_DefaultsToNonEmptyGuid()
    {
        // Act
        var entity = new TestEntity();

        // Assert
        Assert.NotEqual(Guid.Empty, entity.Id);
    }

    [Fact]
    public void Id_TwoSeparateInstances_HaveDifferentIds()
    {
        // Act
        var entity1 = new TestEntity();
        var entity2 = new TestEntity();

        // Assert
        Assert.NotEqual(entity1.Id, entity2.Id);
    }

    [Fact]
    public void Id_CanBeOverwritten()
    {
        // Arrange
        var entity = new TestEntity();
        var newId = Guid.NewGuid();

        // Act
        entity.Id = newId;

        // Assert
        Assert.Equal(newId, entity.Id);
    }
}
