using Hibrygame.Enums;
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
        board.Positions[3, 3].Piece = new Pawn(ColorEnum.White);

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
        board.Positions[3, 3].Piece = new Pawn(ColorEnum.White);
        
        
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
        board.Positions[6, 4].Piece = new Rook(ColorEnum.Black);
        board.Positions[4, 4].Piece = new Rook(ColorEnum.Black);
        board.Positions[5, 5].Piece = new Pawn(ColorEnum.White);


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
        board.Positions[4, 2].Piece = new Rook(ColorEnum.White);
        board.Positions[5, 2].Piece = new Rook(ColorEnum.Black);
        board.Positions[6, 2].Piece = new Rook(ColorEnum.White);
        board.Positions[5, 1].Piece = new Pawn(ColorEnum.Black);


        // Act
        var piece = new Pawn(ColorEnum.Black);
        piece.HasAlreadyOneMove = true;
        var result = piece.GetPossibleMove(board, new Position(5, 1));

        // Assert
        Assert.NotNull(result.possibleMoves);
        Assert.Equal(2, result.possibleMoves.Count);
    }
}