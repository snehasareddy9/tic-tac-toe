namespace Backend;

public static class AiPlayer
{
    // Random.Shared is thread-safe (.NET 6+). The old static Random was not,
    // and concurrent /moves requests could corrupt its internal state.

    // Priority per spec:
    //   1. If O can win, play the winning move
    //   2. If X can win next, block X
    //   3. Take center
    //   4. Take a corner
    //   5. Take any available cell
    public static int PickMove(string[] board, string me)
    {
        var opp = me == "X" ? "O" : "X";

        var win = FindWinningMove(board, me);
        if (win >= 0) return win;

        var block = FindWinningMove(board, opp);
        if (block >= 0) return block;

        if (string.IsNullOrEmpty(board[4])) return 4;

        var corners = new[] { 0, 2, 6, 8 }.Where(i => string.IsNullOrEmpty(board[i])).ToArray();
        if (corners.Length > 0) return corners[Random.Shared.Next(corners.Length)];

        var sides = new[] { 1, 3, 5, 7 }.Where(i => string.IsNullOrEmpty(board[i])).ToArray();
        if (sides.Length > 0) return sides[Random.Shared.Next(sides.Length)];

        return -1;
    }

    private static int FindWinningMove(string[] board, string player)
    {
        for (int i = 0; i < 9; i++)
        {
            if (!string.IsNullOrEmpty(board[i])) continue;
            var copy = (string[])board.Clone();
            copy[i] = player;
            if (Game.CheckWinner(copy).winner == player) return i;
        }
        return -1;
    }
}
