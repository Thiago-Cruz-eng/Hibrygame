using Xunit;

namespace Hibrygame.Test.Hibrygame;

public class RookTests
{
    [Fact]
    public void GetMovesRook_AloneFirstMove_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.positions[3, 3].piece = new Rook(ColorEnum.White);

        // Act
        var piece = new Rook(ColorEnum.White);
        var result = piece.GetPossibleMove(board, new Position(3,3));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(14, result.Count);
    }

    [Fact]
    public void GetMovesRook_WhenAnotherEnemyIsInWay_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.positions[6, 4].piece = new Rook(ColorEnum.Black);
        board.positions[7, 4].piece = new Rook(ColorEnum.Black);
        board.positions[7, 0].piece = new Rook(ColorEnum.White);


        // Act
        var piece = new Rook(ColorEnum.White);
        var result = piece.GetPossibleMove(board, new Position(7, 0));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(11, result.Count);
    }
    
    [Fact]
    public void GetMovesRook_WhenAnotherFriendIsInWay_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.positions[7, 4].piece = new Rook(ColorEnum.White);
        board.positions[7, 0].piece = new Rook(ColorEnum.White);


        // Act
        var piece = new Rook(ColorEnum.White);
        var result = piece.GetPossibleMove(board, new Position(7, 0));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(10, result.Count);
    }
    
    [Fact]
    public void GetMovesRook_WhenNoPossibilities_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.positions[3, 2].piece = new Rook(ColorEnum.White);
        board.positions[4, 3].piece = new Rook(ColorEnum.White);
        board.positions[3, 4].piece = new Rook(ColorEnum.White);
        board.positions[2, 3].piece = new Rook(ColorEnum.White);
        
        board.positions[3, 3].piece = new Rook(ColorEnum.White);


        // Act
        var piece = new Rook(ColorEnum.White);
        var result = piece.GetPossibleMove(board, new Position(3, 3));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.Count);
    }
}