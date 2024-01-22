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
        board.positions[5,5].piece = new King(ColorEnum.White);

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
        board.positions[4, 5].piece = new Rook(ColorEnum.White);
        board.positions[5,5].piece = new King(ColorEnum.White);


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
        board.positions[6,6].piece = new Rook(ColorEnum.White);
        board.positions[7,6].piece = new Rook(ColorEnum.White);
        board.positions[6,7].piece = new Rook(ColorEnum.White);
        board.positions[7,7].piece = new King(ColorEnum.White);


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
        board.positions[6,6].piece = new Rook(ColorEnum.Black);
        board.positions[7,6].piece = new Rook(ColorEnum.Black);
        board.positions[6,7].piece = new Rook(ColorEnum.Black);
        board.positions[7,7].piece = new King(ColorEnum.White);

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
        board.positions[6,6].piece = new Rook(ColorEnum.Black);
        board.positions[7,6].piece = new Rook(ColorEnum.Black);
        board.positions[6,7].piece = new Rook(ColorEnum.White);
        board.positions[7,7].piece = new King(ColorEnum.White);

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
        board.positions[3,1].piece = new Rook(ColorEnum.Black);
        board.positions[0,6].piece = new Rook(ColorEnum.Black);
        board.positions[3,6].piece = new King(ColorEnum.White);

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
        board.positions[3,1].piece = new Rook(ColorEnum.Black);
        board.positions[7,4].piece = new Queen(ColorEnum.White);
        board.positions[3,6].piece = new King(ColorEnum.White);

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
        board.positions[3,1].piece = new Rook(ColorEnum.Black);
        board.positions[7,4].piece = new Queen(ColorEnum.White);
        board.positions[3,4].piece = new Rook(ColorEnum.White);
        board.positions[3,6].piece = new King(ColorEnum.White);
        
        // Act
        var piece = new Rook(ColorEnum.White);
        var positions = piece.GetPossibleMove(board, new Position(3,4));
        var move = await Move.MakeMove(board, positions.possibleMoves, new Position(0, 4), new Position(3, 4) {
            squareColor = ColorEnum.White,
            piece = piece
        });
        
        // Assert
        Assert.False(move);
        Assert.IsType<Rook>(board.positions[3, 4].piece);
        Assert.Equal(ColorEnum.White, board.positions[3, 4].piece.Color);
    }
    
    [Fact]
    public async void GetMovesKing_WhenIsInSaveByOwnMove_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.positions[3,1].piece = new Rook(ColorEnum.Black);
        board.positions[7,4].piece = new Queen(ColorEnum.White);
        board.positions[3,4].piece = new Rook(ColorEnum.White);
        board.positions[0,6].piece = new King(ColorEnum.White);
        
        // Act
        //outra forma de "colocar a peça no tabuleiro"
        var piece = new Rook(ColorEnum.White);
        var positions = piece.GetPossibleMove(board, new Position(3,4));
        var move = await Move.MakeMove(board, positions.possibleMoves, new Position(6, 4), new Position(3, 4)
        {
            squareColor = ColorEnum.White,
            piece = piece
        });
        
        // Assert
        Assert.True(move);
        Assert.IsType<Rook>(board.positions[6, 4].piece);
        Assert.Equal(ColorEnum.White, board.positions[6, 4].piece.Color);
    }
}