using Hibrygame.Enums;
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
        board.Positions[3, 3].Piece = new Bishop(ColorEnum.White);

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
        board.Positions[3, 3].Piece = new Rook(ColorEnum.Black);
        board.Positions[5, 5].Piece = new Bishop(ColorEnum.White);


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
        board.Positions[6, 6].Piece = new Rook(ColorEnum.White);
        board.Positions[7, 6].Piece = new Rook(ColorEnum.White);
        board.Positions[6, 7].Piece = new Rook(ColorEnum.White);
        board.Positions[7, 7].Piece = new Bishop(ColorEnum.White);


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
        board.Positions[7, 6].Piece = new Rook(ColorEnum.Black);
        board.Positions[6, 7].Piece = new Rook(ColorEnum.Black);
        board.Positions[7, 7].Piece = new Bishop(ColorEnum.White);

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
        board.Positions[2, 2].Piece = new Rook(ColorEnum.Black);
        board.Positions[4, 3].Piece = new Rook(ColorEnum.Black);
        board.Positions[7, 1].Piece = new Rook(ColorEnum.White);
        board.Positions[1, 7].Piece = new Rook(ColorEnum.Black);
        board.Positions[7, 7].Piece = new Queen(ColorEnum.White);
        board.Positions[4, 4].Piece = new Bishop(ColorEnum.White);

        // Act
        var piece = new Bishop(ColorEnum.White);
        var result = piece.GetPossibleMove(board, new Position(4,4));

        // Assert
        Assert.NotNull(result.possibleMoves);
        Assert.Equal(9, result.possibleMoves.Count);
    }
}