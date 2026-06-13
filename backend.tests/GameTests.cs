using Backend;
using Xunit;

namespace Backend.Tests;

public class GameTests
{
    // --- Valid / invalid moves ---

    [Fact]
    public void ValidMove_PlacesXAndSwitchesTurn()
    {
        var g = new Game("pvp");
        Assert.True(g.TryMove(0, "X", out var err));
        Assert.Null(err);
        Assert.Equal("X", g.Board[0]);
        Assert.Equal("O", g.CurrentPlayer);
        Assert.Equal(GameStatus.InProgress, g.Status);
        Assert.Single(g.Moves);
        Assert.Equal(1, g.Moves[0].MoveNumber);
    }

    [Fact]
    public void InvalidMove_OutOfRange_IsRejected_AndTurnUnchanged()
    {
        var g = new Game("pvp");
        Assert.False(g.TryMove(99, "X", out var err));
        Assert.NotNull(err);
        Assert.Equal("X", g.CurrentPlayer);
        Assert.Empty(g.Moves);
    }

    [Fact]
    public void InvalidMove_OccupiedCell_IsRejected()
    {
        var g = new Game("pvp");
        g.TryMove(0, "X", out _);
        Assert.False(g.TryMove(0, "O", out var err));
        Assert.NotNull(err);
        Assert.Equal("O", g.CurrentPlayer);
    }

    [Fact]
    public void InvalidMove_WrongPlayer_IsRejected()
    {
        var g = new Game("pvp");
        Assert.False(g.TryMove(0, "O", out var err));
        Assert.Contains("not", err, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("X", g.CurrentPlayer);
    }

    [Fact]
    public void InvalidMove_NullPlayer_IsRejected()
    {
        var g = new Game("pvp");
        Assert.False(g.TryMove(0, null, out var err));
        Assert.Equal("Player must be X or O", err);
        Assert.Empty(g.Moves);
    }

    [Fact]
    public void InvalidMove_EmptyPlayer_IsRejected()
    {
        var g = new Game("pvp");
        Assert.False(g.TryMove(0, "", out var err));
        Assert.Equal("Player must be X or O", err);
        Assert.Empty(g.Moves);
    }

    [Fact]
    public void InvalidMove_LowercasePlayer_IsRejected()
    {
        var g = new Game("pvp");
        Assert.False(g.TryMove(0, "x", out var err));
        Assert.Equal("Player must be X or O", err);
        Assert.Empty(g.Moves);
    }

    [Fact]
    public void InvalidMove_BogusPlayerSymbol_IsRejected()
    {
        var g = new Game("pvp");
        Assert.False(g.TryMove(0, "Z", out var err));
        Assert.Equal("Player must be X or O", err);
        Assert.Empty(g.Moves);
    }

    [Fact]
    public void MoveAfterGameCompletion_IsRejected()
    {
        var g = PlayTopRowWinForX();
        Assert.Equal(GameStatus.Won, g.Status);
        Assert.False(g.TryMove(5, "O", out var err));
        Assert.Equal("Game is already over", err);
    }

    // --- Turn switching ---

    [Fact]
    public void TurnAlternates_AfterEachValidMove()
    {
        var g = new Game("pvp");
        g.TryMove(0, "X", out _);
        Assert.Equal("O", g.CurrentPlayer);
        g.TryMove(1, "O", out _);
        Assert.Equal("X", g.CurrentPlayer);
        g.TryMove(2, "X", out _);
        Assert.Equal("O", g.CurrentPlayer);
    }

    // --- Win detection ---

    [Fact]
    public void RowWin_IsDetected_AndCellsHighlighted()
    {
        var g = PlayTopRowWinForX();
        Assert.Equal(GameStatus.Won, g.Status);
        Assert.Equal("X", g.Winner);
        Assert.Equal(new[] { 0, 1, 2 }, g.WinningCells);
    }

    [Fact]
    public void ColumnWin_IsDetected()
    {
        var g = new Game("pvp");
        // X col 0, O scatter
        g.TryMove(0, "X", out _); g.TryMove(1, "O", out _);
        g.TryMove(3, "X", out _); g.TryMove(2, "O", out _);
        g.TryMove(6, "X", out _);
        Assert.Equal(GameStatus.Won, g.Status);
        Assert.Equal("X", g.Winner);
        Assert.Equal(new[] { 0, 3, 6 }, g.WinningCells);
    }

    [Fact]
    public void DiagonalWin_IsDetected()
    {
        var g = new Game("pvp");
        g.TryMove(0, "X", out _); g.TryMove(1, "O", out _);
        g.TryMove(4, "X", out _); g.TryMove(2, "O", out _);
        g.TryMove(8, "X", out _);
        Assert.Equal("X", g.Winner);
        Assert.Equal(new[] { 0, 4, 8 }, g.WinningCells);
    }

    // --- Draw detection ---

    [Fact]
    public void Draw_IsDetected_WhenBoardFullWithNoWinner()
    {
        var g = new Game("pvp");
        // X O X
        // X O O
        // O X X
        int[] order = { 0, 1, 2, 4, 3, 5, 7, 6, 8 };
        string[] who = { "X", "O", "X", "O", "X", "O", "X", "O", "X" };
        for (int i = 0; i < order.Length; i++) g.TryMove(order[i], who[i], out _);
        Assert.Equal(GameStatus.Draw, g.Status);
        Assert.Null(g.Winner);
    }

    // --- Reset ---

    [Fact]
    public void Reset_ClearsBoardAndHistory_AndResetsCurrentPlayerToX()
    {
        var g = PlayTopRowWinForX();
        g.Reset();
        Assert.All(g.Board, c => Assert.True(string.IsNullOrEmpty(c)));
        Assert.Empty(g.Moves);
        Assert.Equal("X", g.CurrentPlayer);
        Assert.Equal(GameStatus.InProgress, g.Status);
        Assert.Null(g.Winner);
        Assert.Null(g.WinningCells);
    }

    // --- Undo: PvP ---

    [Fact]
    public void Undo_InPvp_RemovesOnlyLastMove_AndRestoresTurn()
    {
        var g = new Game("pvp");
        g.TryMove(0, "X", out _); // X
        g.TryMove(4, "O", out _); // O
        Assert.True(g.TryUndo(out _));
        Assert.Equal("", g.Board[4]);
        Assert.Equal("X", g.Board[0]);
        Assert.Equal("O", g.CurrentPlayer);
        Assert.Single(g.Moves);
    }

    [Fact]
    public void Undo_WithNoMoves_IsRejected()
    {
        var g = new Game("pvp");
        Assert.False(g.TryUndo(out var err));
        Assert.NotNull(err);
    }

    [Fact]
    public void Undo_AfterGameOver_IsRejected_OptionA()
    {
        var g = PlayTopRowWinForX();
        Assert.False(g.TryUndo(out var err));
        Assert.Contains("over", err, StringComparison.OrdinalIgnoreCase);
    }

    // --- Undo: AI ---

    [Fact]
    public void Undo_InAi_RemovesBothPlayerAndComputerMoves()
    {
        // Simulate the same sequence the endpoint produces: X then O.
        var g = new Game("ai");
        g.TryMove(0, "X", out _);
        g.TryMove(4, "O", out _); // pretend computer chose center
        Assert.True(g.TryUndo(out _));
        Assert.Equal("", g.Board[0]);
        Assert.Equal("", g.Board[4]);
        Assert.Empty(g.Moves);
        Assert.Equal("X", g.CurrentPlayer);
    }

    // --- Helpers ---

    private static Game PlayTopRowWinForX()
    {
        var g = new Game("pvp");
        g.TryMove(0, "X", out _);
        g.TryMove(3, "O", out _);
        g.TryMove(1, "X", out _);
        g.TryMove(4, "O", out _);
        g.TryMove(2, "X", out _);
        return g;
    }
}
