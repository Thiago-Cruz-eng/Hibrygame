using Hibrygame.Enums;
using Xunit;

namespace Hibrygame.Test.Hibrygame;

public class BoardTests
{
    [Fact]
    public void StartBoard_Initializes_Correctly()
    {
        // Arrange
        var board = new Board();

        // Act
        var result = board.StartBoard();
        board.Positions[0, 2].Piece = new Rook(ColorEnum.Black);
        board.Positions[5, 5].Piece = new King(ColorEnum.White);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<List<Position>>(result);
        Assert.Equal(64, result.Count); 

        Assert.IsType<Rook>(board.Positions[0, 2].Piece);
        Assert.Equal(ColorEnum.Black, board.Positions[0, 2].Piece.Color);

        Assert.IsType<King>(board.Positions[5, 5].Piece);
        Assert.Equal(ColorEnum.White, board.Positions[5, 5].Piece.Color);
    }
    
    [Fact]
    public void GetPositionsPlaced_Correctly()
    {
        // Arrange
        var board = new Board();

        // Act
        board.StartBoard();
        board.Positions[4, 1].Piece = new Rook(ColorEnum.White); 
        board.Positions[0, 2].Piece = new Rook(ColorEnum.Black);
        var result = board.GetPositionsPlaced();

        // Assert
        Assert.NotNull(result);
        Assert.IsType<Rook>(board.Positions[0, 2].Piece);
        Assert.Equal(ColorEnum.Black, board.Positions[0, 2].Piece.Color);
        Assert.IsType<Rook>(board.Positions[4,1].Piece);
        Assert.Equal(ColorEnum.White, board.Positions[4,1].Piece.Color);
        Assert.Equal(ColorEnum.None, board.Positions[0, 3].SquareColor);
        Assert.Equal(2, result.Count);
    }
}