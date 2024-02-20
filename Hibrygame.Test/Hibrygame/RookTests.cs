using Hibrygame.Enums;
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
        board.Positions[3, 3].Piece = new Rook(ColorEnum.White);

        // Act
        var piece = new Rook(ColorEnum.White);
        var result = piece.GetPossibleMove(board, new Position(3,3));

        // Assert
        Assert.NotNull(result.possibleMoves);
        Assert.Equal(14, result.possibleMoves.Count);
    }

    [Fact]
    public void GetMovesRook_WhenAnotherEnemyIsInWay_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.Positions[6, 4].Piece = new Rook(ColorEnum.Black);
        board.Positions[7, 4].Piece = new Rook(ColorEnum.Black);
        board.Positions[7, 0].Piece = new Rook(ColorEnum.White);


        // Act
        var piece = new Rook(ColorEnum.White);
        var result = piece.GetPossibleMove(board, new Position(7, 0));

        // Assert
        Assert.NotNull(result.possibleMoves);
        Assert.Equal(11, result.possibleMoves.Count);
    }
    
    [Fact]
    public void GetMovesRook_WhenAnotherFriendIsInWay_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.Positions[7, 4].Piece = new Rook(ColorEnum.White);
        board.Positions[7, 0].Piece = new Rook(ColorEnum.White);


        // Act
        var piece = new Rook(ColorEnum.White);
        var result = piece.GetPossibleMove(board, new Position(7, 0));

        // Assert
        Assert.NotNull(result.possibleMoves);
        Assert.Equal(10, result.possibleMoves.Count);
    }
    
    [Fact]
    public void GetMovesRook_WhenNoPossibilities_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.Positions[3, 2].Piece = new Rook(ColorEnum.White);
        board.Positions[4, 3].Piece = new Rook(ColorEnum.White);
        board.Positions[3, 4].Piece = new Rook(ColorEnum.White);
        board.Positions[2, 3].Piece = new Rook(ColorEnum.White);
        
        board.Positions[3, 3].Piece = new Rook(ColorEnum.White);


        // Act
        var piece = new Rook(ColorEnum.White);
        var result = piece.GetPossibleMove(board, new Position(3, 3));

        // Assert
        Assert.NotNull(result.possibleMoves);
        Assert.Equal(0, result.possibleMoves.Count);
    }
}