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
        board.positions[0, 2].piece = new Rook(ColorEnum.Black);
        board.positions[5, 5].piece = new King(ColorEnum.White);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<List<Position>>(result);
        Assert.Equal(64, result.Count); 

        Assert.IsType<Rook>(board.positions[0, 2].piece);
        Assert.Equal(ColorEnum.Black, board.positions[0, 2].piece.Color);

        Assert.IsType<King>(board.positions[5, 5].piece);
        Assert.Equal(ColorEnum.White, board.positions[5, 5].piece.Color);
    }
    
    [Fact]
    public void GetPositionsPlaced_Correctly()
    {
        // Arrange
        var board = new Board();

        // Act
        board.StartBoard();
        board.positions[4, 1].piece = new Rook(ColorEnum.White); 
        board.positions[0, 2].piece = new Rook(ColorEnum.Black);
        var result = board.GetPositionsPlaced();

        // Assert
        Assert.NotNull(result);
        Assert.IsType<Rook>(board.positions[0, 2].piece);
        Assert.Equal(ColorEnum.Black, board.positions[0, 2].piece.Color);
        Assert.IsType<Rook>(board.positions[4,1].piece);
        Assert.Equal(ColorEnum.White, board.positions[4,1].piece.Color);
        Assert.Equal(ColorEnum.None, board.positions[0, 3].squareColor);
        Assert.Equal(2, result.Count);
    }
}