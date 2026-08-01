---
name: motor-de-xadrez
description: >
  Engine de xadrez em C# puro (Hibrygame/Logic): notação dupla algébrico/índice com o eixo
  Column invertido, inicialização do tabuleiro, cálculo de movimento por direção e passos,
  o desvio especial do cavalo, detecção de xeque por efeito colateral em IsInCheckState,
  aplicação de jogada com rollback de auto-xeque e os casos de borda conhecidos. Use ao
  alterar regra de peça, calcular ou depurar movimento possível, converter coordenada,
  investigar xeque, mexer em Move/Board/Position/Common ou em qualquer peça
  (Pawn, Knight, Bishop, Rook, Queen, King).
metadata:
  type: domain-skill
---

# Motor de xadrez

> **Mantendo esta skill**
>
> Atualize sempre que o comportamento da engine mudar de propósito. Refactor que preserva o
> comportamento não exige mudança; mudança de regra, sim. Divergência entre skill e código sem
> decisão registrada é escalada para o humano, não resolvida por conta própria.

## Visão geral

`Hibrygame/` é biblioteca C# pura: tabuleiro, posições, seis peças, cálculo de movimento e
detecção de xeque. Não conhece HTTP, sala, jogador nem persistência — e **não pode passar a
conhecer** (Princípio I da constituição). Regra de xadrez nova entra aqui, com teste em
`Hibrygame.Test/Hibrygame/`, nunca no `ChessHub`.

Arquivos:

```
Hibrygame/Logic/
  Position.cs   coordenada + peça na casa + notação algébrica
  Board.cs      Position[8,8], montagem inicial, acesso por índice ou algébrico
  Piece.cs      classe abstrata: Type, Color, IsInCheckState, HasAlreadyOneMove
  Pawn.cs Knight.cs Bishop.cs Rook.cs Queen.cs King.cs
  Move.cs       CalculatePossibleMove, MakeMove, IsKingInCheck (o coração)
  Common.cs     limites do tabuleiro, validade, oponentes, PositionComparer
  Enums/        ColorEnum, PieceEnum, Direction, EnumConverter/
```

## Notação: dois sistemas, um deles invertido

Esta é a fonte número um de bug na engine. Decore:

```
File  = 'a' + Row        Row    = file - 'a'      Row cresce com a coluna do xadrez (a→h)
Rank  = 8 - Column       Column = 8 - rank        Column cresce ao DESCER o rank (8→1)
```

- `Column = 7` → rank 1 → primeira fileira das **brancas**.
- `Column = 0` → rank 8 → primeira fileira das **pretas**.
- `Row` é o arquivo (`a`..`h`), **não** a linha. O nome engana.

Consequência nas direções: `Direction.North` é `Column - steps` (sobe o rank) e
`Direction.East` é `Row + steps` (anda de `a` para `h`). Ver `Move.CalculateNewPosition`.

Converta **só na borda** com os helpers, nunca com aritmética espalhada:

```csharp
Position.FromAlgebraic("e4");                        // lança ArgumentException se inválido
Position.TryFromAlgebraic("e4", out var pos);         // preferido na borda de entrada
Position.ToIndices("e4");                             // (row, column)
pos.Algebraic;  pos.File;  pos.Rank;                  // getters de saída
board.GetPositionByAlgebraic("e4");                   // casa real do tabuleiro
```

**Cuidado:** `TryFromAlgebraic` cria uma `Position` **nova e solta**, sem `Piece` e sem
`SquareColor`. Ela serve para obter `Row`/`Column`; para operar no jogo, busque a casa real com
`board.GetPositionInBoard(row, column)`. Trocar as duas é bug garantido — ver a seção de
igualdade abaixo.

## Montagem do tabuleiro

```csharp
var board = new Board();
board.StartBoard();              // instancia as 64 Positions (obrigatório primeiro)
board.MakePieceInInitialState(); // pinta as casas e coloca as peças
```

`StartBoard()` sem `MakePieceInInitialState()` devolve tabuleiro vazio; a ordem inversa gera
`NullReferenceException`. `GameRoom.Start()` chama as duas na ordem certa — replique isso em
qualquer construção nova de tabuleiro.

Cor da casa: `(Row + Column) % 2 == 1 ? Black : White`.
Posições iniciais em `Board.DefinePiece`: `Column 7` = back rank branca,
`Column 6` = peões brancos, `Column 1` = peões pretos, `Column 0` = back rank preta. Na back
rank, `Row 0/7` = torre, `1/6` = cavalo, `2/5` = bispo, `3` = rainha, `4` = rei.

## Cálculo de movimento

Toda peça delega para o mesmo motor, variando apenas **direções** e **número de casas**:

| Peça | Direções | Casas |
|---|---|---|
| `Pawn` | `South`/`SouthEast`/`SouthWest` se preta, `North`/`NorthEast`/`NorthWest` se branca | `HasAlreadyOneMove ? 1 : 2` |
| `Knight` | `North`, `South`, `East`, `West` | `2` (o desvio especial entra aqui) |
| `Bishop` | as 4 diagonais | `8` |
| `Rook` | as 4 ortogonais | `8` |
| `Queen` / `King` | as 8 | `8` / `1` |

```csharp
var (moves, trigger) = piece.GetPossibleMove(board, pos);
```

`trigger` é a peça que disparou o cálculo, **exceto** quando o cálculo alcança o rei inimigo:
nesse caso `trigger` é o rei em xeque e `moves` deixa de ser "para onde esta peça vai" e passa a
ser "movimentos que resolvem o xeque" (fuga do rei + defesas amigas). Quem consome precisa saber
qual dos dois recebeu — o `ChessHub` hoje descarta `trigger` (`var (moves, _) = ...`).

Regras embutidas em `Move.CalculatePossibleMove`:

- **Peão só captura na diagonal** — direção diagonal com casa de destino vazia faz `break`.
- **Bloqueio por peça amiga** — `break` antes de incluir a casa.
- **Captura encerra a direção** — a casa inimiga entra em `possibleMoves` e o laço para (via
  `previousEnemyPosition`), então não se atravessa peça.
- **`CalculateNewPosition` devolve a própria `initialPosition`** quando o destino sai do tabuleiro
  ou é inválido. Não devolve `null`: por isso o código compara `pos != initialPosition` em vez de
  checar nulo.
- **`HighlightedPosition = true`** é marcado em todos os movimentos calculados e limpo em
  `MakeMove`. É estado de UI vivendo dentro da engine — não confie nele para lógica.

### O desvio do cavalo (área crítica)

O cavalo não tem direção própria: anda 2 casas em ortogonal e o motor, no passo `i == squares`,
**cria um `Knight` temporário na casa intermediária** (`newPosition.Piece = new Knight(...)`),
calcula os dois braços perpendiculares e depois tenta limpar a peça temporária. Isso muta o
tabuleiro durante o cálculo e é a origem dos casos de borda conhecidos (DT-11 em
`docs/debito-tecnico.md`). O retorno também é diferente: o cavalo devolve `knightPossibleMoves`,
não `possibleMoves`.

Ao mexer em cálculo de movimento: rode `dotnet test Hibrygame.Test` inteiro, não só o teste da
peça alterada. O teste `KnightTests.GetMovesKnight_AfterOneMove_Correctly` está `Skip` — não o
reative sem corrigir a aritmética do teste **e** a engine.

## Detecção de xeque — por efeito colateral

`Move.IsKingInCheck(board, color)` **não** calcula xeque diretamente:

1. localiza o rei da cor e zera `king.Piece.IsInCheckState`;
2. varre todas as peças do oponente chamando `GetPossibleMove` e **descarta o resultado**;
3. devolve `king.Piece.IsInCheckState`.

Quem marca a flag é `CalculatePossibleMove`, ao alcançar o rei inimigo (bloco
`newPosition.Piece?.Type == PieceEnum.King`), via `VerifyKingMovementationCheck`. Ou seja: a
detecção de xeque depende de um efeito colateral dentro do cálculo de movimento.

**Implicação prática:** qualquer alteração em `CalculatePossibleMove` pode quebrar a detecção de
xeque sem que nenhum teste de movimento falhe. Ao mexer lá, rode também os testes de xeque
(`KingTests`) e teste explicitamente `IsKingInCheck`.

## Aplicação de jogada

```csharp
var ok = await Move.MakeMove(board, possibleMoves, target, source);
```

1. `possibleMoves.Contains(target, new Common.PositionComparer())` — comparação **por
   coordenada**; falso ⇒ retorna `false` sem tocar no tabuleiro;
2. move a peça (destino recebe `source.Piece`, origem vira `null`);
3. `IsKingInCheck` da cor que moveu; se der xeque, **desfaz manualmente os dois quadrados** e
   retorna `false`;
4. limpa `HighlightedPosition` de todas as casas;
5. se a peça é peão, marca `HasAlreadyOneMove = true`.

Consequências ao evoluir:

- Campo novo de estado em `Piece` precisa entrar no rollback do passo 3, ou jogada rejeitada
  deixa estado sujo.
- `MakeMove` é `async` mas não faz I/O — `IsKingInCheck` devolve `Task.FromResult`. Não é ponto
  de extensão para persistência.
- Não há promoção, roque nem en passant. `HasAlreadyOneMove` existe só para o avanço duplo do
  peão; não use como "peça já se moveu" para roque sem revisar quem escreve a flag (hoje só o
  peão).

## Igualdade de `Position` (armadilha silenciosa)

`Position` **não** sobrescreve `Equals`/`GetHashCode`. Existe `Common.PositionComparer`
(compara `Row`/`Column`), mas só `Move.MakeMove` o usa. `VerifyKingMovementationCheck` e
`PossiblePiecesHelpersToKingCheck` usam `List.Contains`/`Remove` **sem** comparador — igualdade
por referência. Funciona por acidente enquanto todas as posições saírem do mesmo array
`Board.Positions`; quebra quando alguém injeta uma `Position` criada por
`TryFromAlgebraic`. Ver DT-12.

Regra prática: ao comparar posições, **sempre** passe `new Common.PositionComparer()` ou compare
`Row`/`Column` explicitamente (é o que o `ChessHub` faz:
`possibleMoves.Any(p => p.Row == target.Row && p.Column == target.Column)`).

## Enums

`ColorEnum { Black, White, None }`, `PieceEnum { Pawn, Bishop, Knight, Rook, Queen, King, None }`,
`Direction { North, South, East, West, NorthEast, SouthEast, NorthWest, SouthWest }`.

Os três têm `[JsonConverter(typeof(EnumStringConverter<>))]` do `Newtonsoft.Json` com valores
`EnumMember` minúsculos — **e o de `ColorEnum` está errado** (`White` → `"bhite"`, `None` →
`"white"`, DT-10). O contrato externo do jogo usa `.ToString()` (PascalCase), então isso hoje não
vaza; não introduza serialização do enum pelo conversor sem corrigir primeiro.

## Restrições e pontos cegos

- Sem xeque-mate, empate, desistência e relógio. `GameRoom.Finished` nunca vira `true` (DT-13).
- Sem promoção, roque, en passant.
- Sem histórico de jogadas nem notação PGN/FEN: o tabuleiro é o único estado.
- `Board.GetPositionsPlacedInBoard()` é alias de `GetPositionsPlaced()` (DT-08).
- `Hibrygame.csproj` referencia pacotes de ASP.NET Core sem usar (DT-01) — não adicione mais.
