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
    
    // As quatro asserções abaixo foram corrigidas durante a Fase 1 do refactor.
    // As expectativas antigas (3, 2, 4 e 7) documentavam o comportamento quebrado do
    // gerador: ao encontrar o rei inimigo durante a varredura, ele abandonava a peça
    // consultada e devolvia as casas de fuga DO REI. Uma consulta sobre a torre preta
    // respondia sobre o rei branco.

    [Fact]
    public void GetMovesKing_BoxedInByThreeDefendedRooks_HasNoLegalMove()
    {
        // Arrange — rei branco em h1, torres pretas em g1, g2 e h2.
        // Está em xeque e cada captura possível cai numa casa defendida
        // por outra torre: xeque-mate, zero lances legais.
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
        Assert.Empty(result.possibleMoves);
    }

    [Fact]
    public void GetMovesKing_InCheckWithEveryCaptureDefended_HasNoLegalMove()
    {
        // Arrange — rei branco em h1 com a própria torre em g1 tapando a fuga.
        // As torres pretas g2 e h2 defendem-se mutuamente, então nenhuma pode
        // ser capturada, e a torre h2 mantém o xeque pela coluna h.
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
        Assert.Empty(result.possibleMoves);
    }

    [Fact]
    public void GetMovesRook_GivingCheck_AnswersAboutTheRookAndNotTheKing()
    {
        // Arrange — torre preta em d7 dá xeque ao rei branco em d2.
        var board = new Board();
        board.StartBoard();
        board.Positions[3,1].Piece = new Rook(ColorEnum.Black);
        board.Positions[0,6].Piece = new Rook(ColorEnum.Black);
        board.Positions[3,6].Piece = new King(ColorEnum.White);

        // Act
        var piece = new Rook(ColorEnum.Black);
        var result = piece.GetPossibleMove(board, new Position(3,1));

        // Assert — d8; d6..d2 pela coluna, parando no rei; e7..h7; c7..a7.
        Assert.NotNull(result.possibleMoves);
        Assert.Equal(13, result.possibleMoves.Count);
    }

    [Fact]
    public void GetMovesRook_GivingCheckWithEnemyQueenElsewhere_AnswersAboutTheRook()
    {
        // Arrange — a dama branca em h4 não intercepta nem a coluna d nem a 7ª fileira,
        // logo não altera em nada os lances da torre preta em d7.
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
        Assert.Equal(13, result.possibleMoves.Count);
    }

    [Fact]
    public void GetMovesKing_WhenIsInTreatByOwnMove_Correctly()
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
        var move = Move.MakeMove(board, positions.possibleMoves, new Position(0, 4), new Position(3, 4) {
            SquareColor = ColorEnum.White,
            Piece = piece
        });
        
        // Assert
        Assert.False(move);
        Assert.IsType<Rook>(board.Positions[3, 4].Piece);
        Assert.Equal(ColorEnum.White, board.Positions[3, 4].Piece!.Color);
    }
    
    [Fact]
    public void GetMovesKing_WhenIsInSaveByOwnMove_Correctly()
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
        var move = Move.MakeMove(board, positions.possibleMoves, new Position(6, 4), new Position(3, 4)
        {
            SquareColor = ColorEnum.White,
            Piece = piece
        });
        
        // Assert
        Assert.True(move);
        Assert.IsType<Rook>(board.Positions[6, 4].Piece);
        Assert.Equal(ColorEnum.White, board.Positions[6, 4].Piece!.Color);
    }
}