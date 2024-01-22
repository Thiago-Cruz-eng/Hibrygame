using Xunit;

namespace Hibrygame.Test.Hibrygame;

public class BishopTests
{
    [Fact]
    public void GetMovesBishop_Alone_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.positions[3, 3].piece = new Bishop(ColorEnum.White);

        // Act
        var piece = new Bishop(ColorEnum.Black);
        var result = piece.GetPossibleMove(board, new Position(3,3));

        // Assert
        Assert.NotNull(result.possibleMoves);
        Assert.Equal(13, result.possibleMoves.Count);
    }

    [Fact]
    public void GetMovesBishop_WhenAnotherIsInWay_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.positions[3, 3].piece = new Rook(ColorEnum.Black);
        board.positions[5, 5].piece = new Bishop(ColorEnum.White);


        // Act
        var piece = new Bishop(ColorEnum.Black);
        var result = piece.GetPossibleMove(board, new Position(5, 5));

        // Assert
        Assert.NotNull(result.possibleMoves);
        Assert.Equal(8, result.possibleMoves.Count);
    }

    [Fact]
    public void GetMovesBishop_WithNoPossibleMoves_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.positions[6, 6].piece = new Rook(ColorEnum.White);
        board.positions[7, 6].piece = new Rook(ColorEnum.White);
        board.positions[6, 7].piece = new Rook(ColorEnum.White);
        board.positions[7, 7].piece = new Bishop(ColorEnum.White);


        // Act
        var piece = new Bishop(ColorEnum.Black);
        var result = piece.GetPossibleMove(board, new Position(7, 7));

        // Assert
        Assert.NotNull(result.possibleMoves);
        Assert.Empty(result.possibleMoves);
    }

    [Fact]
    public void GetMovesBishop_WithPossibleWithAllOpponentAcrossMoves_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.positions[7, 6].piece = new Rook(ColorEnum.Black);
        board.positions[6, 7].piece = new Rook(ColorEnum.Black);
        board.positions[7, 7].piece = new Bishop(ColorEnum.White);

        // Act
        var piece = new Bishop(ColorEnum.White);
        var result = piece.GetPossibleMove(board, new Position(7, 7));

        // Assert
        Assert.NotNull(result.possibleMoves);
        Assert.Equal(7, result.possibleMoves.Count);
    }

    [Fact]
    public void GetMovesBishop_WithPossibleOpponentAcross_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.positions[2, 2].piece = new Rook(ColorEnum.Black);
        board.positions[4, 3].piece = new Rook(ColorEnum.Black);
        board.positions[7, 1].piece = new Rook(ColorEnum.White);
        board.positions[1, 7].piece = new Rook(ColorEnum.Black);
        board.positions[7, 7].piece = new Queen(ColorEnum.White);
        board.positions[4, 4].piece = new Bishop(ColorEnum.White);

        // Act
        var piece = new Bishop(ColorEnum.White);
        var result = piece.GetPossibleMove(board, new Position(4,4));

        // Assert
        Assert.NotNull(result.possibleMoves);
        Assert.Equal(9, result.possibleMoves.Count);
    }
}