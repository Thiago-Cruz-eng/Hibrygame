using Hibrygame.Enums;
using Xunit;

namespace Hibrygame.Test.Hibrygame;

public class CommonTests
{
    [Fact]
    public void GetOpponentBlackPositions_Correctly()
    {
        //Arrange
        var board = new Board();
        board.StartBoard();
        var piece = new King(ColorEnum.Black);
        board.positions[2, 3].piece = piece;
        board.positions[2, 4].piece = piece;
        board.positions[2, 5].piece = piece;
        
        // Act
        var result = Common.GetOpponentPositions(board, ColorEnum.White);

        //Assert
        Assert.Equal(3, result.Count);
    }
    
    [Fact]
    public void GetOpponentWhitePositions_Correctly()
    {
        //Arrange
        var board = new Board();
        board.StartBoard();
        var piece = new King(ColorEnum.White);
        board.positions[2, 3].piece = piece;
        board.positions[2, 4].piece = piece;
        board.positions[2, 5].piece = piece;
        
        // Act
        var result = Common.GetOpponentPositions(board, ColorEnum.Black);

        //Assert
        Assert.Equal(3, result.Count);
    }
}