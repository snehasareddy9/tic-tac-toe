using Backend;
using Xunit;

namespace Backend.Tests;

public class ScoreboardTests
{
    [Fact]
    public void RecordsXWin()
    {
        var sb = new Scoreboard();
        sb.Record(PlayXWin());
        var snap = sb.Snapshot();
        Assert.Equal(1, snap.X);
        Assert.Equal(0, snap.O);
        Assert.Equal(0, snap.Draws);
    }

    [Fact]
    public void RecordsDraw()
    {
        var sb = new Scoreboard();
        sb.Record(PlayDraw());
        Assert.Equal(1, sb.Snapshot().Draws);
    }

    [Fact]
    public void DoesNotRecordInProgressGame()
    {
        var sb = new Scoreboard();
        var g = new Game("pvp");
        g.TryMove(0, "X", out _);
        sb.Record(g);
        Assert.Equal(0, sb.Snapshot().X);
    }

    [Fact]
    public void RecordsCompletedGame_OnlyOnce()
    {
        var sb = new Scoreboard();
        var g = PlayXWin();
        sb.Record(g);
        sb.Record(g);
        sb.Record(g);
        Assert.Equal(1, sb.Snapshot().X);
    }

    [Fact]
    public void Reset_ZeroesAllCounters()
    {
        var sb = new Scoreboard();
        sb.Record(PlayXWin());
        sb.Reset();
        var snap = sb.Snapshot();
        Assert.Equal(0, snap.X);
        Assert.Equal(0, snap.O);
        Assert.Equal(0, snap.Draws);
    }

    [Fact]
    public void Forget_AllowsSameGameIdToBeCountedAgain()
    {
        // Reproduces the "Reset Game then play again" dedupe bug.
        // A game keeps its id across a board-level reset, so without Forget()
        // the second completion would be silently dropped.
        var sb = new Scoreboard();
        var g = new Game("game-1", "pvp");
        PlayXWinOn(g);
        sb.Record(g);
        Assert.Equal(1, sb.Snapshot().X);

        g.Reset();
        sb.Forget(g.Id);
        PlayXWinOn(g);
        sb.Record(g);
        Assert.Equal(2, sb.Snapshot().X);
    }

    private static void PlayXWinOn(Game g)
    {
        g.TryMove(0, "X", out _); g.TryMove(3, "O", out _);
        g.TryMove(1, "X", out _); g.TryMove(4, "O", out _);
        g.TryMove(2, "X", out _);
    }

    private static Game PlayXWin()
    {
        var g = new Game("pvp");
        g.TryMove(0, "X", out _); g.TryMove(3, "O", out _);
        g.TryMove(1, "X", out _); g.TryMove(4, "O", out _);
        g.TryMove(2, "X", out _);
        return g;
    }

    private static Game PlayDraw()
    {
        var g = new Game("pvp");
        int[] order = { 0, 1, 2, 4, 3, 5, 7, 6, 8 };
        string[] who = { "X", "O", "X", "O", "X", "O", "X", "O", "X" };
        for (int i = 0; i < order.Length; i++) g.TryMove(order[i], who[i], out _);
        return g;
    }
}
