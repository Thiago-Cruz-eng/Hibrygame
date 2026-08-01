using System.Collections.Concurrent;
using Hibrygame;
using Hibrygame.Enums;

namespace Orchestrator.Infra.SignalR;

public class GameRoom
{
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
