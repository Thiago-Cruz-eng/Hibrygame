using Xunit;

namespace Hibrygame.Test.Hibrygame;

public class BoardTests
{
    [Fact]
    public void StartBoard_Initializes_Correctly()
    {
        // Arrange
        var board = new Board();

        // Act
        var result = board.StartBoard();
        board.positions[5, 3].piece = new King(ColorEnum.White);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<List<Position>>(result);
        Assert.Equal(64, result.Count); 

        Assert.IsType<Rook>(board.positions[0, 2].piece);
        Assert.Equal(ColorEnum.Black, board.positions[0, 2].piece.Color);

        Assert.IsType<King>(board.positions[5, 5].piece);
        Assert.Equal(ColorEnum.White, board.positions[5, 5].piece.Color);
    }
}