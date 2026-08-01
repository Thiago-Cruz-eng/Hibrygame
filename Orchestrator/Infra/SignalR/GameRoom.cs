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

    /// <inheritdoc cref="Serialized{T}(Func{T})"/>
    public async Task<T> Serialized<T>(Func<Task<T>> action)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            return await action().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public bool IsFull => Players.Count >= 2;

    public ColorEnum? TryAssignColor(string connectionId, string playerName)
    {
        if (Players.ContainsKey(connectionId))
            return Players[connectionId].Color;

        if (IsFull) return null;

        var color = Players.Values.Any(p => p.Color == ColorEnum.White)
            ? ColorEnum.Black
            : ColorEnum.White;

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
