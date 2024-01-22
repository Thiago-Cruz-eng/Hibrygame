using Hibrygame.Enums;
using Xunit;

namespace Hibrygame.Test.Hibrygame;

public class QueenTests
{
    [Fact]
    public void GetMovesQueen_Alone_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.positions[5, 5].piece = new Queen(ColorEnum.White);

        // Act
        var piece = new Queen(ColorEnum.Black);
        var result = piece.GetPossibleMove(board, new Position(5,5));

        // Assert
        Assert.NotNull(result.possibleMoves);
        Assert.Equal(25, result.possibleMoves.Count);
    }
    
    [Fact]
    public void GetMovesQueen_WhenAnotherIsInWay_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.positions[4, 5].piece = new Rook(ColorEnum.White);
        board.positions[5, 5].piece = new Queen(ColorEnum.White);


        // Act
        var piece = new Queen(ColorEnum.Black);
        var result = piece.GetPossibleMove(board, new Position(5,5));

        // Assert
        Assert.NotNull(result.possibleMoves);
        Assert.Equal(20, result.possibleMoves.Count);
    }
    
    [Fact]
    public void GetMovesQueen_WithNoPossibleMoves_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.positions[6,6].piece = new Rook(ColorEnum.White);
        board.positions[7,6].piece = new Rook(ColorEnum.White);
        board.positions[6,7].piece = new Rook(ColorEnum.White);
        board.positions[7,7].piece = new King(ColorEnum.White);


        // Act
        var piece = new Queen(ColorEnum.Black);
        var result = piece.GetPossibleMove(board, new Position(7,7));

        // Assert
        Assert.NotNull(result.possibleMoves);
        Assert.Empty(result.possibleMoves);
    }
    
    [Fact]
    public void GetMovesQueen_WithPossibleWithAllOpponentAcrossMoves_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.positions[6,6].piece = new Rook(ColorEnum.Black);
        board.positions[7,6].piece = new Rook(ColorEnum.Black);
        board.positions[6,7].piece = new Rook(ColorEnum.Black);
        board.positions[7,7].piece = new Queen(ColorEnum.White);

        // Act
        var piece = new Queen(ColorEnum.White);
        var result = piece.GetPossibleMove(board, new Position(7,7));

        // Assert
        Assert.NotNull(result.possibleMoves);
        Assert.Equal(3, result.possibleMoves.Count);
    }
    
    [Fact]
    public void GetMovesQueen_WithPossibleOpponentAcross_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.positions[6,6].piece = new Rook(ColorEnum.Black);
        board.positions[7,6].piece = new Rook(ColorEnum.Black);
        board.positions[6,7].piece = new Rook(ColorEnum.White);
        board.positions[7,7].piece = new Queen(ColorEnum.White);

        // Act
        var piece = new Queen(ColorEnum.White);
        var result = piece.GetPossibleMove(board, new Position(7,7));

        // Assert
        Assert.NotNull(result.possibleMoves);
        Assert.Equal(2, result.possibleMoves.Count);
    }
}