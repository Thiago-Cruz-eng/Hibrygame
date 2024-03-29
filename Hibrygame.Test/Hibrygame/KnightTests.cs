using Hibrygame.Enums;
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
        board.Positions[7,0].Piece = new Knight(ColorEnum.White);

        // Act
        var piece = new Knight(ColorEnum.White);
        var result = piece.GetPossibleMove(board, new Position(7,0));

        // Assert
        Assert.NotNull(result.possibleMoves);
        Assert.Equal(2, result.possibleMoves.Count);
    }

    [Fact]
    public void GetMovesKnight_WhenAnotherTwoEnemyIsInWay_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.Positions[4,7].Piece = new Rook(ColorEnum.Black);
        board.Positions[6,7].Piece = new Rook(ColorEnum.Black);
        board.Positions[5, 5].Piece = new Knight(ColorEnum.White);


        // Act
        var piece = new Knight(ColorEnum.White);
        var result = piece.GetPossibleMove(board, new Position(5, 5));

        // Assert
        Assert.NotNull(result.possibleMoves);
        Assert.Equal(8, result.possibleMoves.Count);
    }
    
    [Fact]
    public void GetMovesKnight_WhenAnotherFourFriendIsInWay_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.Positions[4,7].Piece = new Rook(ColorEnum.White);
        board.Positions[6,7].Piece = new Rook(ColorEnum.White);
        board.Positions[7,4].Piece = new Rook(ColorEnum.White);
        board.Positions[7,6].Piece = new Rook(ColorEnum.White);
        board.Positions[5, 5].Piece = new Knight(ColorEnum.White);


        // Act
        var piece = new Knight(ColorEnum.White);
        var result = piece.GetPossibleMove(board, new Position(5, 5));

        // Assert
        Assert.NotNull(result.possibleMoves);
        Assert.Equal(4, result.possibleMoves.Count);
    }
    
    [Fact]
    public void GetMovesKnight_WhenNoPossibilitiesMoves_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.Positions[4,7].Piece = new Rook(ColorEnum.White);
        board.Positions[6,7].Piece = new Rook(ColorEnum.White);
        board.Positions[7,4].Piece = new Rook(ColorEnum.White);
        board.Positions[7,6].Piece = new Rook(ColorEnum.White);
        board.Positions[4,3].Piece = new Rook(ColorEnum.White);
        board.Positions[6,3].Piece = new Rook(ColorEnum.White);
        board.Positions[3,4].Piece = new Rook(ColorEnum.White);
        board.Positions[3,6].Piece = new Rook(ColorEnum.White);
        board.Positions[5, 5].Piece = new Knight(ColorEnum.White);


        // Act
        var piece = new Knight(ColorEnum.White);
        var result = piece.GetPossibleMove(board, new Position(5, 5));

        // Assert
        Assert.NotNull(result.possibleMoves);
        Assert.Equal(0, result.possibleMoves.Count);
    }
    
    [Fact]
    public void GetMovesKnight_WhenEnemyInWay_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.Positions[4,7].Piece = new Rook(ColorEnum.Black);
        board.Positions[6,7].Piece = new Rook(ColorEnum.Black);
        board.Positions[7,4].Piece = new Rook(ColorEnum.Black);
        board.Positions[7,6].Piece = new Rook(ColorEnum.Black);
        board.Positions[4,3].Piece = new Rook(ColorEnum.Black);
        board.Positions[6,3].Piece = new Rook(ColorEnum.Black);
        board.Positions[3,4].Piece = new Rook(ColorEnum.Black);
        board.Positions[3,6].Piece = new Rook(ColorEnum.Black);
        board.Positions[5, 5].Piece = new Knight(ColorEnum.White);


        // Act
        var piece = new Knight(ColorEnum.White);
        var result = piece.GetPossibleMove(board, new Position(5, 5));

        // Assert
        Assert.NotNull(result.possibleMoves);
        Assert.Equal(8, result.possibleMoves.Count);
    }
}