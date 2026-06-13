using System.Collections.Concurrent;
using Backend;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<Scoreboard>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();

var games = new ConcurrentDictionary<string, Game>();

// --- games ---

app.MapPost("/api/games", (NewGameRequest? req, Scoreboard sb) =>
{
    var mode = (req?.Mode ?? "pvp").ToLowerInvariant();
    if (mode != "pvp" && mode != "ai") mode = "pvp";
    Game game;
    do { game = new Game(mode); } while (!games.TryAdd(game.Id, game));   // defensive against any id collision
    return Results.Ok(game.ToDto(sb));
});

app.MapGet("/api/games/{id}", (string id, Scoreboard sb) =>
{
    if (!games.TryGetValue(id, out var g)) return Results.NotFound();
    lock (g.SyncRoot) { return Results.Ok(g.ToDto(sb)); }   // consistent snapshot
});

app.MapPost("/api/games/{id}/moves", (string id, MoveRequest? req, Scoreboard sb) =>
{
    if (!games.TryGetValue(id, out var g)) return Results.NotFound();

    // Validate the request shape before touching game state.
    if (req is null) return Results.BadRequest(new { error = "Request body is required" });
    if (req.Index is null || req.Index < 0 || req.Index > 8)
        return Results.BadRequest(new { error = "Cell index out of range" });
    if (req.Player is not "X" and not "O")
        return Results.BadRequest(new { error = "Player must be X or O" });

    lock (g.SyncRoot)
    {
        if (!g.TryMove(req.Index.Value, req.Player, out var error))
            return Results.BadRequest(new { error });

        if (g.Status != GameStatus.InProgress) sb.Record(g);

        // Computer auto-responds in AI mode.
        if (g.Mode == "ai" && g.Status == GameStatus.InProgress && g.CurrentPlayer == "O")
        {
            var aiMove = AiPlayer.PickMove(g.Board, "O");
            if (aiMove >= 0) g.TryMove(aiMove, "O", out _);
            if (g.Status != GameStatus.InProgress) sb.Record(g);
        }
        return Results.Ok(g.ToDto(sb));
    }
});

app.MapPost("/api/games/{id}/undo", (string id, Scoreboard sb) =>
{
    if (!games.TryGetValue(id, out var g)) return Results.NotFound();
    lock (g.SyncRoot)
    {
        if (!g.TryUndo(out var error)) return Results.BadRequest(new { error });
        return Results.Ok(g.ToDto(sb));
    }
});

app.MapPost("/api/games/{id}/reset", (string id, Scoreboard sb) =>
{
    if (!games.TryGetValue(id, out var g)) return Results.NotFound();
    lock (g.SyncRoot)
    {
        sb.Forget(g.Id);   // allow this game to count again after the reset
        g.Reset();
        return Results.Ok(g.ToDto(sb));
    }
});

// --- scoreboard ---

app.MapGet("/api/scoreboard", (Scoreboard sb) => Results.Ok(sb.Snapshot()));

app.MapPost("/api/scoreboard/reset", (Scoreboard sb) =>
{
    sb.Reset();
    return Results.Ok(sb.Snapshot());
});

app.Run("http://localhost:5050");

// Exposed so the test project can reference WebApplicationFactory<Program> in the future if needed.
public partial class Program { }
