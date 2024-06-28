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
    
    [Fact]
    public void GetMovesKnight_AfterOneMove_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.Positions[5,5].Piece = new Knight(ColorEnum.White);


        // Act
        var piece = new Knight(ColorEnum.White);
        var result = piece.GetPossibleMove(board, new Position(5, 5));
        var pieces = board.GetPositionsPlacedInBoard();
        var move = board.MakeMove(board, result.possibleMoves, board.Positions[4,7], board.Positions[5,5] );
        var piecess = board.GetPositionsPlacedInBoard();
        var result2 = piece.GetPossibleMove(board, new Position(4, 7));
        var piecesss = board.GetPositionsPlacedInBoard();
        var move2 = board.MakeMove(board, result2.possibleMoves, board.Positions[3,5], board.Positions[4,7] );
        var piecessss = board.GetPositionsPlacedInBoard();
        var result3 = piece.GetPossibleMove(board, new Position(3, 5));
        var piecesssss = board.GetPositionsPlacedInBoard();
        var move3 = board.MakeMove(board, result3.possibleMoves, board.Positions[1,4], board.Positions[3,5] );
        var piecessssss = board.GetPositionsPlacedInBoard();


        // Assert
        Assert.NotNull(result2.possibleMoves);
        Assert.Equal(8, result2.possibleMoves.Count);
    }
}