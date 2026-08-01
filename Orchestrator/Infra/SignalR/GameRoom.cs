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
    public Board Board { get; } = new();
    public ConcurrentDictionary<string, PlayerSlot> Players { get; } = new();
    public ColorEnum CurrentTurn { get; private set; } = ColorEnum.White;
    public bool Started { get; private set; }
    public bool Finished { get; private set; }

    public GameRoom(string name)
    {
        Name = name;
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

    public void Start()
    {
        if (Started) return;
        Board.StartBoard();
        Board.MakePieceInInitialState();
        Started = true;
        CurrentTurn = ColorEnum.White;
    }

    public void SwitchTurn() =>
        CurrentTurn = CurrentTurn == ColorEnum.White ? ColorEnum.Black : ColorEnum.White;

    public void Finish() => Finished = true;

    public record PlayerSlot(string Name, ColorEnum Color);
}
