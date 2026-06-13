namespace Backend;

public enum GameStatus { InProgress, Won, Draw }

public record MoveRecord(int MoveNumber, string Player, int Index);

public class Game
{
    private static readonly int[][] Lines = new[]
    {
        new[]{0,1,2}, new[]{3,4,5}, new[]{6,7,8},
        new[]{0,3,6}, new[]{1,4,7}, new[]{2,5,8},
        new[]{0,4,8}, new[]{2,4,6}
    };

    // Per-game lock. Endpoints take this lock around the full move/undo/reset/snapshot
    // operation so a single request runs atomically against a single game,
    // including the AI auto-response and the scoreboard recording.
    public object SyncRoot { get; } = new();

    public string Id { get; }
    public string Mode { get; }
    public string[] Board { get; private set; } = new string[9];
    public string CurrentPlayer { get; private set; } = "X";
    public GameStatus Status { get; private set; } = GameStatus.InProgress;
    public string? Winner { get; private set; }
    public int[]? WinningCells { get; private set; }
    public List<MoveRecord> Moves { get; } = new();

    public Game(string mode)
    {
        Id = Guid.NewGuid().ToString("N");   // full 32-char GUID — no truncation, no collision risk
        Mode = mode;
    }

    // Constructor for tests that need a deterministic id
    public Game(string id, string mode) { Id = id; Mode = mode; }

    public void Reset()
    {
        Board = new string[9];
        CurrentPlayer = "X";
        Status = GameStatus.InProgress;
        Winner = null;
        WinningCells = null;
        Moves.Clear();
    }

    public bool TryMove(int index, string? player, out string? error)
    {
        error = null;
        if (Status != GameStatus.InProgress) { error = "Game is already over"; return false; }
        if (index < 0 || index > 8) { error = "Cell index out of range"; return false; }
        if (player != "X" && player != "O") { error = "Player must be X or O"; return false; }
        if (!string.IsNullOrEmpty(Board[index])) { error = "Cell is already taken"; return false; }
        if (player != CurrentPlayer) { error = $"It is not {player}'s turn"; return false; }

        Board[index] = CurrentPlayer;
        Moves.Add(new MoveRecord(Moves.Count + 1, CurrentPlayer, index));
        RecomputeStatus();

        if (Status == GameStatus.InProgress)
            CurrentPlayer = CurrentPlayer == "X" ? "O" : "X";
        return true;
    }

    // Undo policy = Option A (see README): undo disallowed once game is Won or Draw.
    public bool TryUndo(out string? error)
    {
        error = null;
        if (Status != GameStatus.InProgress)
        {
            error = "Cannot undo after the game is over";
            return false;
        }
        if (Moves.Count == 0) { error = "No moves to undo"; return false; }

        if (Mode == "ai")
        {
            // Remove the last move and, if it was O (the computer), also remove the X before it.
            // Spec: undo removes the AI move and the human move together.
            var last = Moves[^1];
            Moves.RemoveAt(Moves.Count - 1);
            Board[last.Index] = "";
            if (last.Player == "O" && Moves.Count > 0)
            {
                var prev = Moves[^1];
                Moves.RemoveAt(Moves.Count - 1);
                Board[prev.Index] = "";
            }
        }
        else
        {
            var last = Moves[^1];
            Moves.RemoveAt(Moves.Count - 1);
            Board[last.Index] = "";
        }

        // Whoever has made fewer moves goes next.
        CurrentPlayer = Moves.Count % 2 == 0 ? "X" : "O";
        RecomputeStatus();
        return true;
    }

    private void RecomputeStatus()
    {
        var (winner, line) = CheckWinner(Board);
        Winner = winner;
        WinningCells = line;
        if (winner != null) Status = GameStatus.Won;
        else if (Board.All(c => !string.IsNullOrEmpty(c))) Status = GameStatus.Draw;
        else Status = GameStatus.InProgress;
    }

    public static (string? winner, int[]? line) CheckWinner(string[] b)
    {
        foreach (var l in Lines)
        {
            var a = b[l[0]];
            if (!string.IsNullOrEmpty(a) && a == b[l[1]] && a == b[l[2]]) return (a, l);
        }
        return (null, null);
    }

    public object ToDto(Scoreboard sb) => new
    {
        id = Id,
        mode = Mode,
        board = Board.Select(c => c ?? "").ToArray(),
        currentPlayer = CurrentPlayer,
        status = Status.ToString(),
        winner = Winner,
        winningCells = WinningCells,
        moves = Moves.Select(m => new
        {
            moveNumber = m.MoveNumber,
            player = m.Player,
            row = m.Index / 3 + 1,
            col = m.Index % 3 + 1,
            index = m.Index
        }).ToArray(),
        scoreboard = sb.Snapshot()
    };
}
