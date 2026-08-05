using Hibrygame.Enums;
using Xunit;

namespace Hibrygame.Test.Hibrygame;

/// <summary>
/// Estes testes marcavam HasAlreadyOneMove numa instancia de Pawn criada a parte, e nao
/// na peca que estava no tabuleiro. Funcionava porque o alcance do peao era passado pela
/// propria instancia chamadora ate Move. Agora que Move e a fonte unica de verdade e le a
/// flag da peca que ocupa a casa, o cenario tem de ser montado no tabuleiro — que e o que
/// os testes sempre quiseram dizer.
/// </summary>
public class PawnTests
{
    private static Pawn PawnThatHasMoved(ColorEnum color) => new(color) { HasAlreadyOneMove = true };

    [Fact]
    public void GetMovesPawnWhite_AloneFirstMove_Correctly()
    {
        // Arrange
        var board = new Board();
        board.StartBoard();
        board.Positions[3, 3].Piece = new Pawn(ColorEnum.White);

        // Act
        var result = board.Positions[3, 3].Piece!.GetPossibleMove(board, board.Positions[3, 3]);

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
        board.Positions[3, 3].Piece = PawnThatHasMoved(ColorEnum.White);

        // Act
        var result = board.Positions[3, 3].Piece!.GetPossibleMove(board, board.Positions[3, 3]);

        // Assert
        Assert.NotNull(result.possibleMoves);
        Assert.Single(result.possibleMoves);
    }

    [Fact]
    public void GetMovesPawnWhite_WhenAnotherTwoIsInWay_Correctly()
    {
        // Arrange — peao branco em f3, torres pretas em g4 e e4:
        // avanca para f4 e captura nas duas diagonais.
        var board = new Board();
        board.StartBoard();
        board.Positions[6, 4].Piece = new Rook(ColorEnum.Black);
        board.Positions[4, 4].Piece = new Rook(ColorEnum.Black);
        board.Positions[5, 5].Piece = PawnThatHasMoved(ColorEnum.White);

        // Act
        var result = board.Positions[5, 5].Piece!.GetPossibleMove(board, board.Positions[5, 5]);

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
        board.Positions[5, 1].Piece = PawnThatHasMoved(ColorEnum.Black);

        // Act
        var result = board.Positions[5, 1].Piece!.GetPossibleMove(board, board.Positions[5, 1]);

        // Assert
        Assert.NotNull(result.possibleMoves);
        Assert.Equal(2, result.possibleMoves.Count);
    }
}
