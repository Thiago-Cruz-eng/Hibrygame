using Xunit;

namespace Hibrygame.Test.Hibrygame;

public class PawnTests
{
    [Fact]
    public void GetMovesPawnWhite_AloneFirstMove_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.positions[3, 3].piece = new Pawn(ColorEnum.White);

        // Act
        var piece = new Pawn(ColorEnum.White);
        var result = piece.GetPossibleMove(board, new Position(3,3));

        // Assert
        Assert.NotNull(result.possibleMoves);
        Assert.Equal(2, result.possibleMoves.Count);
    }
    
    [Fact]
    public void GetMovesPawnWhite_AloneAfterFirstMove_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.positions[3, 3].piece = new Pawn(ColorEnum.White);
        
        
        // Act
        var piece = new Pawn(ColorEnum.White);
        piece.HasAlreadyOneMove = true;
        var result = piece.GetPossibleMove(board, new Position(3,3));

        // Assert
        Assert.NotNull(result.possibleMoves);
        Assert.Equal(1, result.possibleMoves.Count);
    }

    [Fact]
    public void GetMovesPawnWhite_WhenAnotherTwoIsInWay_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.positions[6, 4].piece = new Rook(ColorEnum.Black);
        board.positions[4, 4].piece = new Rook(ColorEnum.Black);
        board.positions[5, 5].piece = new Pawn(ColorEnum.White);


        // Act
        var piece = new Pawn(ColorEnum.White);
        piece.HasAlreadyOneMove = true;
        var result = piece.GetPossibleMove(board, new Position(5, 5));

        // Assert
        Assert.NotNull(result.possibleMoves);
        Assert.Equal(3, result.possibleMoves.Count);
    }
    
    [Fact]
    public void GetMovesPawnBlack_WhenAnotherOneIsInWay_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.positions[4, 2].piece = new Rook(ColorEnum.White);
        board.positions[5, 2].piece = new Rook(ColorEnum.Black);
        board.positions[6, 2].piece = new Rook(ColorEnum.White);
        board.positions[5, 1].piece = new Pawn(ColorEnum.Black);


        // Act
        var piece = new Pawn(ColorEnum.Black);
        piece.HasAlreadyOneMove = true;
        var result = piece.GetPossibleMove(board, new Position(5, 1));

        // Assert
        Assert.NotNull(result.possibleMoves);
        Assert.Equal(2, result.possibleMoves.Count);
    }
}