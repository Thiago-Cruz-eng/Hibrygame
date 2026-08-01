using Xunit;

namespace Hibrygame.Test.Hibrygame;

/// <summary>
/// Coordinate system:
///   Row   = file index (0='a' .. 7='h'), drives File property
///   Column = rank offset from top (0=rank8 .. 7=rank1), drives Rank property
///   Algebraic = "{File}{Rank}"  e.g. new Position(4,4) → "e4"
/// </summary>
public class PositionTests
{
    // ---------------------------------------------------------------
    // File / Rank / Algebraic round-trip from (row, col) inputs
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(0, 7, "a1")]
    [InlineData(7, 7, "h1")]
    [InlineData(0, 0, "a8")]
    [InlineData(7, 0, "h8")]
    [InlineData(4, 4, "e4")]
    public void Algebraic_FromRowAndColumn_ReturnsExpectedNotation(int row, int col, string expected)
    {
        // Arrange
        var position = new Position(row, col);

        // Act
        var result = position.Algebraic;

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void File_Row0_ReturnsA()
    {
        // Arrange
        var position = new Position(0, 4);

        // Act & Assert
        Assert.Equal('a', position.File);
    }

    [Fact]
    public void File_Row7_ReturnsH()
    {
        // Arrange
        var position = new Position(7, 4);

        // Act & Assert
        Assert.Equal('h', position.File);
    }

    [Fact]
    public void Rank_Column7_Returns1()
    {
        // Arrange
        var position = new Position(3, 7);

        // Act & Assert
        Assert.Equal(1, position.Rank);
    }

    [Fact]
    public void Rank_Column0_Returns8()
    {
        // Arrange
        var position = new Position(3, 0);

        // Act & Assert
        Assert.Equal(8, position.Rank);
    }

    // ---------------------------------------------------------------
    // FromAlgebraic round-trip
    // ---------------------------------------------------------------

    [Fact]
    public void FromAlgebraic_E4_ReturnsRow4Column4()
    {
        // Arrange & Act
        var position = Position.FromAlgebraic("e4");

        // Assert
        Assert.Equal(4, position.Row);
        Assert.Equal(4, position.Column);
    }

    [Fact]
    public void FromAlgebraic_A1_ReturnsRow0Column7()
    {
        // Arrange & Act
        var position = Position.FromAlgebraic("a1");

        // Assert
        Assert.Equal(0, position.Row);
        Assert.Equal(7, position.Column);
    }

    [Fact]
    public void FromAlgebraic_H8_ReturnsRow7Column0()
    {
        // Arrange & Act
        var position = Position.FromAlgebraic("h8");

        // Assert
        Assert.Equal(7, position.Row);
        Assert.Equal(0, position.Column);
    }

    [Fact]
    public void FromAlgebraic_AlgebraicRoundTrip_IsSymmetric()
    {
        // Arrange
        var original = new Position(4, 4); // "e4"

        // Act
        var restored = Position.FromAlgebraic(original.Algebraic);

        // Assert
        Assert.Equal(original.Row, restored.Row);
        Assert.Equal(original.Column, restored.Column);
    }

    // ---------------------------------------------------------------
    // Case insensitivity
    // ---------------------------------------------------------------

    [Fact]
    public void FromAlgebraic_UpperCaseE4_ParsesCorrectly()
    {
        // Arrange & Act
        var position = Position.FromAlgebraic("E4");

        // Assert
        Assert.Equal(4, position.Row);
        Assert.Equal(4, position.Column);
    }

    // ---------------------------------------------------------------
    // FromAlgebraic throws ArgumentException on invalid input
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("i1")]   // file out of range
    [InlineData("a0")]   // rank out of range (0 not valid)
    [InlineData("a9")]   // rank out of range (9 not valid)
    [InlineData("z9")]   // invalid file and rank
    [InlineData("e")]    // too short
    [InlineData("e44")]  // too long
    public void FromAlgebraic_InvalidInput_ThrowsArgumentException(string? notation)
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => Position.FromAlgebraic(notation!));
    }

    // ---------------------------------------------------------------
    // TryFromAlgebraic
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("e4", true)]
    [InlineData("a1", true)]
    [InlineData("h8", true)]
    [InlineData("A8", true)]   // uppercase accepted
    [InlineData("i1", false)]  // file out of range
    [InlineData("a0", false)]  // rank out of range
    [InlineData("a9", false)]  // rank out of range
    [InlineData("z9", false)]  // bad file and rank
    [InlineData("e",  false)]  // too short
    [InlineData("e44", false)] // too long
    [InlineData("", false)]    // empty
    public void TryFromAlgebraic_VariousInputs_ReturnsExpectedResult(string notation, bool expectedSuccess)
    {
        // Arrange & Act
        var result = Position.TryFromAlgebraic(notation, out var position);

        // Assert
        Assert.Equal(expectedSuccess, result);
        if (expectedSuccess)
            Assert.NotNull(position);
        else
            Assert.Null(position);
    }

    [Fact]
    public void TryFromAlgebraic_NullInput_ReturnsFalse()
    {
        // Arrange & Act
        var result = Position.TryFromAlgebraic(null!, out var position);

        // Assert
        Assert.False(result);
        Assert.Null(position);
    }

    // ---------------------------------------------------------------
    // ToIndices
    // ---------------------------------------------------------------

    [Fact]
    public void ToIndices_E4_Returns4And4()
    {
        // Arrange & Act
        var (row, col) = Position.ToIndices("e4");

        // Assert
        Assert.Equal(4, row);
        Assert.Equal(4, col);
    }

    [Fact]
    public void ToIndices_A1_Returns0And7()
    {
        // Arrange & Act
        var (row, col) = Position.ToIndices("a1");

        // Assert
        Assert.Equal(0, row);
        Assert.Equal(7, col);
    }

    [Fact]
    public void ToIndices_H8_Returns7And0()
    {
        // Arrange & Act
        var (row, col) = Position.ToIndices("h8");

        // Assert
        Assert.Equal(7, row);
        Assert.Equal(0, col);
    }
}
