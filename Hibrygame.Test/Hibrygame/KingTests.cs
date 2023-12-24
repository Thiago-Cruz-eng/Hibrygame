using Xunit;

namespace Hibrygame.Test.Hibrygame;

public class KingTests
{
    [Fact]
    public void GetMovesKing_Alone_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();

        // Act
        var piece = new King(ColorEnum.Black);
        var result = piece.GetMovesKing(board, new Position(5,5));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(8, result.Count);
    }
    
    [Fact]
    public void GetMovesKing_WhenAnotherIsInWay_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.positions[4, 5].piece = new Rook(ColorEnum.Black);

        // Act
        var piece = new King(ColorEnum.Black);
        var result = piece.GetMovesKing(board, new Position(5,5));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(7, result.Count);
    }
    
    [Fact]
    public void GetMovesKing_WithNoPossibleMoves_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.positions[6,6].piece = new Rook(ColorEnum.Black);
        board.positions[7,6].piece = new Rook(ColorEnum.Black);
        board.positions[6,7].piece = new Rook(ColorEnum.Black);

        // Act
        var piece = new King(ColorEnum.Black);
        var result = piece.GetMovesKing(board, new Position(7,7));

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }
}