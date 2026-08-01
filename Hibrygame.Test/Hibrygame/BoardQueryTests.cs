using Hibrygame.Enums;
using Xunit;

namespace Hibrygame.Test.Hibrygame;

public class BoardQueryTests
{
    [Fact]
    public void GetOpponentBlackPositions_Correctly()
    {
        //Arrange
        var board = new Board();
        board.StartBoard();
        var piece = new King(ColorEnum.Black);
        board.Positions[2, 3].Piece = piece;
        board.Positions[2, 4].Piece = piece;
        board.Positions[2, 5].Piece = piece;
        
        // Act
        var result = board.GetOpponentPositions(ColorEnum.White);

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
        board.Positions[2, 3].Piece = piece;
        board.Positions[2, 4].Piece = piece;
        board.Positions[2, 5].Piece = piece;
        
        // Act
        var result = board.GetOpponentPositions(ColorEnum.Black);

        //Assert
        Assert.Equal(3, result.Count);
    }
}