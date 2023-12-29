using Xunit;

namespace Hibrygame.Test.Hibrygame;

public class KnightTests
{
        [Fact]
    public void GetMovesKnight_AloneFirstMove_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.positions[7,0].piece = new Knight(ColorEnum.White);

        // Act
        var piece = new Knight(ColorEnum.White);
        var result = piece.GetPossibleMove(board, new Position(7,0));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void GetMovesKnight_WhenAnotherTwoEnemyIsInWay_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.positions[4,7].piece = new Rook(ColorEnum.Black);
        board.positions[6,7].piece = new Rook(ColorEnum.Black);
        board.positions[5, 5].piece = new Knight(ColorEnum.White);


        // Act
        var piece = new Knight(ColorEnum.White);
        var result = piece.GetPossibleMove(board, new Position(5, 5));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(8, result.Count);
    }
    
    [Fact]
    public void GetMovesKnight_WhenAnotherFourFriendIsInWay_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.positions[4,7].piece = new Rook(ColorEnum.White);
        board.positions[6,7].piece = new Rook(ColorEnum.White);
        board.positions[7,4].piece = new Rook(ColorEnum.White);
        board.positions[7,6].piece = new Rook(ColorEnum.White);
        board.positions[5, 5].piece = new Knight(ColorEnum.White);


        // Act
        var piece = new Knight(ColorEnum.White);
        var result = piece.GetPossibleMove(board, new Position(5, 5));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(4, result.Count);
    }
    
    [Fact]
    public void GetMovesKnight_WhenNoPossibilitiesMoves_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.positions[4,7].piece = new Rook(ColorEnum.White);
        board.positions[6,7].piece = new Rook(ColorEnum.White);
        board.positions[7,4].piece = new Rook(ColorEnum.White);
        board.positions[7,6].piece = new Rook(ColorEnum.White);
        board.positions[4,3].piece = new Rook(ColorEnum.White);
        board.positions[6,3].piece = new Rook(ColorEnum.White);
        board.positions[3,4].piece = new Rook(ColorEnum.White);
        board.positions[3,6].piece = new Rook(ColorEnum.White);
        board.positions[5, 5].piece = new Knight(ColorEnum.White);


        // Act
        var piece = new Knight(ColorEnum.White);
        var result = piece.GetPossibleMove(board, new Position(5, 5));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.Count);
    }
    
    [Fact]
    public void GetMovesKnight_WhenEnemyInWay_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.positions[4,7].piece = new Rook(ColorEnum.Black);
        board.positions[6,7].piece = new Rook(ColorEnum.Black);
        board.positions[7,4].piece = new Rook(ColorEnum.Black);
        board.positions[7,6].piece = new Rook(ColorEnum.Black);
        board.positions[4,3].piece = new Rook(ColorEnum.Black);
        board.positions[6,3].piece = new Rook(ColorEnum.Black);
        board.positions[3,4].piece = new Rook(ColorEnum.Black);
        board.positions[3,6].piece = new Rook(ColorEnum.Black);
        board.positions[5, 5].piece = new Knight(ColorEnum.White);


        // Act
        var piece = new Knight(ColorEnum.White);
        var result = piece.GetPossibleMove(board, new Position(5, 5));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(8, result.Count);
    }
}