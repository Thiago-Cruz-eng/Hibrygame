using Hibrygame.Enums;
using Xunit;

namespace Hibrygame.Test.Hibrygame;

public class KingTests
{
    [Fact]
    public void GetMovesKing_Alone_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.Positions[5,5].Piece = new King(ColorEnum.White);

        // Act
        var piece = new King(ColorEnum.Black);
        var result = piece.GetPossibleMove(board, new Position(5,5));

        // Assert
        Assert.NotNull(result.possibleMoves);
        Assert.Equal(8, result.possibleMoves.Count);
    }
    
    [Fact]
    public void GetMovesKing_WhenAnotherIsInWay_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.Positions[4, 5].Piece = new Rook(ColorEnum.White);
        board.Positions[5,5].Piece = new King(ColorEnum.White);


        // Act
        var piece = new King(ColorEnum.Black);
        var result = piece.GetPossibleMove(board, new Position(5,5));

        // Assert
        Assert.NotNull(result.possibleMoves);
        Assert.Equal(7, result.possibleMoves.Count);
    }
    
    [Fact]
    public void GetMovesKing_WithNoPossibleMoves_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.Positions[6,6].Piece = new Rook(ColorEnum.White);
        board.Positions[7,6].Piece = new Rook(ColorEnum.White);
        board.Positions[6,7].Piece = new Rook(ColorEnum.White);
        board.Positions[7,7].Piece = new King(ColorEnum.White);


        // Act
        var piece = new King(ColorEnum.Black);
        var result = piece.GetPossibleMove(board, new Position(7,7));

        // Assert
        Assert.NotNull(result.possibleMoves);
        Assert.Empty(result.possibleMoves);
    }
    
    [Fact]
    public void GetMovesKing_WithPossibleWithAllOpponentAcrossMoves_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.Positions[6,6].Piece = new Rook(ColorEnum.Black);
        board.Positions[7,6].Piece = new Rook(ColorEnum.Black);
        board.Positions[6,7].Piece = new Rook(ColorEnum.Black);
        board.Positions[7,7].Piece = new King(ColorEnum.White);

        // Act
        var piece = new King(ColorEnum.White);
        var result = piece.GetPossibleMove(board, new Position(7,7));

        // Assert
        Assert.NotNull(result.possibleMoves);
        Assert.Equal(3, result.possibleMoves.Count);
    }
    
    [Fact]
    public void GetMovesKing_WithPossibleOpponentAcross_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.Positions[6,6].Piece = new Rook(ColorEnum.Black);
        board.Positions[7,6].Piece = new Rook(ColorEnum.Black);
        board.Positions[6,7].Piece = new Rook(ColorEnum.White);
        board.Positions[7,7].Piece = new King(ColorEnum.White);

        // Act
        var piece = new King(ColorEnum.White);
        var result = piece.GetPossibleMove(board, new Position(7,7));

        // Assert
        Assert.NotNull(result.possibleMoves);
        Assert.Equal(2, result.possibleMoves.Count);
    }
    
    [Fact]
    public void GetMovesKing_WhenIsInTreat_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.Positions[3,1].Piece = new Rook(ColorEnum.Black);
        board.Positions[0,6].Piece = new Rook(ColorEnum.Black);
        board.Positions[3,6].Piece = new King(ColorEnum.White);

        // Act
        var piece = new Rook(ColorEnum.Black);
        var result = piece.GetPossibleMove(board, new Position(3,1));

        // Assert
        Assert.NotNull(result.possibleMoves);
        Assert.Equal(4, result.possibleMoves.Count);
    }
    
    [Fact]
    public void GetMovesKing_WhenIsInTreatWithFriendAside_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.Positions[3,1].Piece = new Rook(ColorEnum.Black);
        board.Positions[7,4].Piece = new Queen(ColorEnum.White);
        board.Positions[3,6].Piece = new King(ColorEnum.White);

        // Act
        var piece = new Rook(ColorEnum.Black);
        var result = piece.GetPossibleMove(board, new Position(3,1));

        // Assert
        Assert.NotNull(result.possibleMoves);
        Assert.Equal(7, result.possibleMoves.Count);
    }
    
    [Fact]
    public async void GetMovesKing_WhenIsInTreatByOwnMove_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.Positions[3,1].Piece = new Rook(ColorEnum.Black);
        board.Positions[7,4].Piece = new Queen(ColorEnum.White);
        board.Positions[3,4].Piece = new Rook(ColorEnum.White);
        board.Positions[3,6].Piece = new King(ColorEnum.White);
        
        // Act
        var piece = new Rook(ColorEnum.White);
        var positions = piece.GetPossibleMove(board, new Position(3,4));
        var move = await Move.MakeMove(board, positions.possibleMoves, new Position(0, 4), new Position(3, 4) {
            SquareColor = ColorEnum.White,
            Piece = piece
        });
        
        // Assert
        Assert.False(move);
        Assert.IsType<Rook>(board.Positions[3, 4].Piece);
        Assert.Equal(ColorEnum.White, board.Positions[3, 4].Piece.Color);
    }
    
    [Fact]
    public async void GetMovesKing_WhenIsInSaveByOwnMove_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.Positions[3,1].Piece = new Rook(ColorEnum.Black);
        board.Positions[7,4].Piece = new Queen(ColorEnum.White);
        board.Positions[3,4].Piece = new Rook(ColorEnum.White);
        board.Positions[0,6].Piece = new King(ColorEnum.White);
        
        // Act
        //outra forma de "colocar a peça no tabuleiro"
        var piece = new Rook(ColorEnum.White);
        var positions = piece.GetPossibleMove(board, new Position(3,4));
        var move = await Move.MakeMove(board, positions.possibleMoves, new Position(6, 4), new Position(3, 4)
        {
            SquareColor = ColorEnum.White,
            Piece = piece
        });
        
        // Assert
        Assert.True(move);
        Assert.IsType<Rook>(board.Positions[6, 4].Piece);
        Assert.Equal(ColorEnum.White, board.Positions[6, 4].Piece.Color);
    }
}