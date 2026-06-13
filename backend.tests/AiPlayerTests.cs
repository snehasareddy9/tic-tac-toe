using Backend;
using Xunit;

namespace Backend.Tests;

public class AiPlayerTests
{
    [Fact]
    public void TakesWinningMove_WhenAvailable()
    {
        // O can win at 2 (top row)
        var board = new[] { "O", "O", "", "X", "X", "", "", "", "" };
        Assert.Equal(2, AiPlayer.PickMove(board, "O"));
    }

    [Fact]
    public void BlocksOpponent_WhenOpponentCanWinNext()
    {
        // X threatens to win at 2; O must block
        var board = new[] { "X", "X", "", "", "O", "", "", "", "" };
        Assert.Equal(2, AiPlayer.PickMove(board, "O"));
    }

    [Fact]
    public void PrefersWinning_OverBlocking()
    {
        // O can win at 2, X can also win at 5; O should take the win.
        var board = new[] { "O", "O", "", "X", "X", "", "", "", "" };
        Assert.Equal(2, AiPlayer.PickMove(board, "O"));
    }

    [Fact]
    public void TakesCenter_WhenNoWinNoBlockAndCenterFree()
    {
        var board = new[] { "X", "", "", "", "", "", "", "", "" };
        Assert.Equal(4, AiPlayer.PickMove(board, "O"));
    }

    [Fact]
    public void TakesCorner_WhenCenterTakenAndNoWinNoBlock()
    {
        var board = new[] { "", "", "", "", "X", "", "", "", "" };
        var move = AiPlayer.PickMove(board, "O");
        Assert.Contains(move, new[] { 0, 2, 6, 8 });
    }

    [Fact]
    public void ReturnsMinusOne_WhenBoardFull()
    {
        var board = new[] { "X", "O", "X", "X", "O", "O", "O", "X", "X" };
        Assert.Equal(-1, AiPlayer.PickMove(board, "O"));
    }
}
