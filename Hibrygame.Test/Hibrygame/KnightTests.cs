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
        Assert.Empty(result.possibleMoves);
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
    public async Task GetMovesKnight_AfterOneMove_Correctly()
    {
        // Este teste estava ignorado por esperar 8 lances a partir de e1. Um cavalo em e1
        // encosta em duas bordas: só existem 4 saltos geometricamente possíveis (c2, d3,
        // f3, g2). A expectativa antiga era impossível; o cenário em si é válido e é o que
        // se verifica agora — o cavalo continua a gerar lances corretos depois de andar.

        // Arrange — cavalo branco em f3.
        var board = new Board();
        board.StartBoard();
        board.Positions[5, 5].Piece = new Knight(ColorEnum.White);

        // Act — f3 -> e1, e depois pergunta os lances a partir de e1.
        var knight = board.Positions[5, 5].Piece!;
        var fromF3 = knight.GetPossibleMove(board, board.Positions[5, 5]);
        var moved = await Move.MakeMove(board, fromF3.possibleMoves, board.Positions[4, 7], board.Positions[5, 5]);
        var fromE1 = knight.GetPossibleMove(board, board.Positions[4, 7]);

        // Assert
        Assert.True(moved);
        Assert.Null(board.Positions[5, 5].Piece);
        Assert.Equal(PieceEnum.Knight, board.Positions[4, 7].Piece!.Type);

        Assert.NotNull(fromE1.possibleMoves);
        Assert.Equal(
            new[] { "c2", "d3", "f3", "g2" },
            fromE1.possibleMoves.Select(p => p.Algebraic).OrderBy(s => s, StringComparer.Ordinal).ToArray());
    }
}