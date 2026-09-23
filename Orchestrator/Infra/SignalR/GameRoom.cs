using System.Collections.Concurrent;
using Hibrygame;
using Hibrygame.Enums;

namespace Orchestrator.Infra.SignalR;

public class GameRoom
{
    /// <summary>
    /// Serializa o acesso a <see cref="Board"/>. Necessario por dois motivos:
    ///
    /// - Avaliar legalidade simula cada lance candidato no tabuleiro e desfaz. Duas
    ///   chamadas simultaneas na mesma sala intercalam simulacao e desfazer, e o
    ///   desfazer de uma apaga a peca da outra.
    /// - Em MakeMove, a verificacao de turno e a troca de turno estao separadas por
    ///   um await; sem serializacao o mesmo jogador consegue dois lances numa vez.
    ///
    /// Use sempre via <see cref="Serialized{T}"/>.
    /// </summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    public string Name { get; }

    /// <summary>
    /// Id do usuário que criou a sala (claim <c>sub</c>), ou <c>null</c> quando não foi possível
    /// identificá-lo.
    ///
    /// <para>
    /// Serve ao teto de salas por usuário em <c>ChessHub.CreateRoom</c>. Não tem efeito nenhum
    /// sobre a partida: a cor e a autoridade de jogar continuam vindo do assento em
    /// <see cref="Players"/>, que é por conexão. Criador não é jogador.
    /// </para>
    ///
    /// <para>
    /// <c>null</c> em teste, onde o <c>HubCallerContext</c> mockado não traz usuário — e sala sem
    /// criador conhecido fica fora tanto da contagem quanto da recuperação de salas abandonadas.
    /// </para>
    /// </summary>
    public string? CreatedBy { get; }

    /// <summary>
    /// Quando a sala foi criada, em UTC. É o que permite distinguir uma sala recém-criada, ainda
    /// esperando o criador entrar, de uma sala abandonada — ver a recuperação em
    /// <c>ChessHub.CreateRoom</c>.
    /// </summary>
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    public Board Board { get; } = new();
    public ConcurrentDictionary<string, PlayerSlot> Players { get; } = new();
    public ColorEnum CurrentTurn { get; private set; } = ColorEnum.White;
    public bool Started { get; private set; }
    public bool Finished { get; private set; }

    /// <summary>
    /// Situacao da partida na vez de <see cref="CurrentTurn"/>, calculada uma vez por
    /// lance e guardada aqui.
    ///
    /// Existe para o snapshot voltar a ser leitura pura. Ao ganhar o campo Outcome, o
    /// BuildSnapshot passou a chamar Move.EvaluateOutcome, que para responder "existe
    /// lance legal?" SIMULA cada candidato no tabuleiro e desfaz. Montar o snapshot
    /// deixou de ser leitura e virou escrita transitoria — e como StartGame o chamava
    /// fora do lock, dois clientes montando snapshot ao mesmo tempo corrompiam o
    /// tabuleiro: aparecia peao branco em a2, a3 e a4 ao mesmo tempo, 34 pecas no total.
    ///
    /// Recalcular a cada leitura tambem era desperdicio: uma varredura completa de lances
    /// legais por snapshot, e o snapshot vai em toda difusao de BoardChanged.
    /// </summary>
    public GameOutcome Outcome { get; private set; } = GameOutcome.InProgress;

    /// <summary>
    /// Cria a sala com o tabuleiro vazio, sem jogadores e sem partida iniciada.
    ///
    /// <para>
    /// O tabuleiro só ganha peças em <see cref="Start"/>, chamado por <c>ChessHub.StartGame</c>
    /// quando a sala enche. Construir a sala não começa partida nenhuma.
    /// </para>
    /// </summary>
    /// <param name="name">Nome da sala, já validado e aparado por <c>ChessHub.CreateRoom</c>.</param>
    /// <param name="createdBy">
    /// Quem criou a sala. Opcional para não obrigar todo teste a inventar um usuário — ver
    /// <see cref="CreatedBy"/>.
    /// </param>
    public GameRoom(string name, string? createdBy = null)
    {
        Name = name;
        CreatedBy = createdBy;
    }

    /// <summary>Executa <paramref name="action"/> com acesso exclusivo ao tabuleiro da sala.</summary>
    public async Task<T> Serialized<T>(Func<T> action)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            return action();
        }
        finally
        {
            _gate.Release();
        }
    }

    // A sobrecarga que recebia Func<Task<T>> existia apenas porque Move.MakeMove e
    // Move.IsKingInCheck devolviam Task sem nunca ter trabalho assincrono. Agora que
    // sao sincronos, o corpo protegido tambem e — e a sobrecarga saiu.

    public bool IsFull => Players.Count >= 2;

    /// <summary>
    /// Reserva um assento para a conexao. <paramref name="preferred"/> e a cor pedida pelo
    /// jogador no lobby: e atendida quando esta livre, e ignorada quando o adversario ja a
    /// tomou. Passar <c>null</c> mantem a ordem de chegada (primeiro entra de brancas).
    /// </summary>
    public ColorEnum? TryAssignColor(string connectionId, string playerName, ColorEnum? preferred = null)
    {
        if (Players.TryGetValue(connectionId, out var existing))
            return existing.Color;

        if (IsFull) return null;

        var taken = Players.Values.Select(p => p.Color).ToHashSet();

        ColorEnum color;
        if (preferred is ColorEnum wanted && wanted != ColorEnum.None && !taken.Contains(wanted))
        {
            color = wanted;
        }
        else
        {
            color = taken.Contains(ColorEnum.White) ? ColorEnum.Black : ColorEnum.White;
        }

        Players[connectionId] = new PlayerSlot(playerName, color);
        return color;
    }

    public void Remove(string connectionId) => Players.TryRemove(connectionId, out _);

    /// <summary>
    /// Monta a posicao inicial. Chame sempre dentro de <see cref="Serialized{T}(Func{T})"/>:
    /// MakePieceInInitialState reescreve as 64 casas e nao pode correr junto com leitura
    /// nem com a simulacao de legalidade.
    /// </summary>
    public void Start()
    {
        if (Started) return;
        Board.StartBoard();
        Board.MakePieceInInitialState();
        Started = true;
        CurrentTurn = ColorEnum.White;
        Outcome = GameOutcome.InProgress;
    }

    public void SwitchTurn() =>
        CurrentTurn = CurrentTurn == ColorEnum.White ? ColorEnum.Black : ColorEnum.White;

    /// <summary>Registra o resultado apurado depois de um lance. Encerra a sala se for terminal.</summary>
    public void SetOutcome(GameOutcome outcome)
    {
        Outcome = outcome;
        if (outcome != GameOutcome.InProgress) Finished = true;
    }

    public void Finish() => Finished = true;

    public record PlayerSlot(string Name, ColorEnum Color);
}
