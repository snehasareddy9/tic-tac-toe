# Tic Tac Toe

A browser-based Tic Tac Toe with an Angular frontend and a .NET backend, running locally.

The backend owns all game rules, validation, move history and the scoreboard. The Angular frontend just renders whatever state the backend returns after each action.

---

## Table of contents

- [Project overview](#project-overview)
- [Tech stack](#tech-stack)
- [Features implemented](#features-implemented)
- [Requirements](#requirements)
- [Quick start](#quick-start)
- [Running each part on its own](#running-each-part-on-its-own)
- [Running tests](#running-tests)
- [Project structure](#project-structure)
- [How the code works](#how-the-code-works)
- [API contract](#api-contract)
- [Design decisions](#design-decisions)
- [Clarifications and assumptions](#clarifications-and-assumptions)
- [Known limitations](#known-limitations)
- [Future improvements](#future-improvements)
- [AI-assisted development notes](#ai-assisted-development-notes)

---

## Project overview

- Standard 3 × 3 board, two-player or vs-computer.
- Backend is the single source of truth for the game state, move history, validation and scoreboard.
- The frontend is intentionally thin: it calls REST endpoints and renders the response.
- All state is in-memory; nothing is persisted to disk.

## Tech stack

| Layer | Choice |
|---|---|
| Frontend | Angular 17 (standalone components), TypeScript |
| Backend | ASP.NET Core 8 (minimal API) |
| Tests | xUnit (.NET) |
| Storage | In-memory (`ConcurrentDictionary` for games, singleton `Scoreboard`) |
| API style | REST / JSON |
| Source control | Git / GitHub |

## Features implemented

- 3 × 3 board, click to place. A taken cell is locked for the rest of the game.
- Two game modes: **2 Players** and **Play Against Computer**.
- Win detection across rows, columns and both diagonals.
- Winning cells highlighted in green.
- Draw detection when all 9 cells are filled without a winner.
- Move history table (Move #, Player, Row, Column).
- **Undo Last Move**:
  - 2-player mode: removes the last single move.
  - Computer mode: removes the computer move and the previous human move together.
  - Disabled once a game is `Won` or `Draw` (see "Design decisions → Clarification 2").
- **Reset Game**: clears board and history for the current game session; **keeps** the scoreboard.
- **New Game**: starts a new game session; also keeps the scoreboard.
- **Mode switch resets the session**: changing the dropdown between **2 Players** and **vs Computer** starts a fresh game and zeroes the scoreboard, since wins from one mode are not comparable to wins from the other.
- **Scoreboard** with X wins / O wins / Draws — owned by the backend, served by the backend, updated exactly once per completed game.
- **Reset Scoreboard** is a separate action from Reset Game.
- Computer move logic priority: win → block → center → corner → any.
- Backend validates and rejects: missing/invalid request body, missing or out-of-range `index`, missing or invalid `player` (only `"X"` or `"O"`), occupied cell, move after game over, move by the wrong player.

## Requirements

- .NET 8 SDK
- Node.js 18+ (npm is bundled)
- Angular 17
- Windows with PowerShell (the helper scripts are `.ps1`)

If `dotnet` or `node` is missing, `start.ps1` prints a red error and exits — it does not auto-install SDKs.

## Quick start

From the project folder:

```
.\start.ps1
```

What it does:
1. Verifies `dotnet`, `node`, `npm` are on PATH.
2. Runs `dotnet restore` for the backend.
3. Runs `npm install` for the frontend if `frontend/node_modules` is missing (first run only, ~1 minute, needs internet).
4. Launches the backend in its own PowerShell window on `http://localhost:5050`.
5. Launches the frontend in its own PowerShell window on `http://localhost:4200` and opens the browser.

If PowerShell's execution policy blocks the script:
```
powershell -ExecutionPolicy Bypass -File .\start.ps1
```

## Running each part on its own

**Backend only:**
```
cd backend
dotnet run
```

**Frontend only:**
```
cd frontend
npm install     # first time only
npm start
```

The frontend expects the backend on `http://localhost:5050`. CORS is already configured to allow `http://localhost:4200`.

## Running tests

```
.\test.ps1
```
or directly:
```
dotnet test backend.tests
```

31 backend unit tests cover the spec scenarios:
- valid move, invalid move (out-of-range, occupied cell, wrong turn, null/empty/lowercase/bogus player), turn switching
- row win, column win, diagonal win, draw
- reset game (and the scoreboard `Forget` so a replayed game can be counted again)
- undo in two-player mode, undo in computer mode, undo after game over (Option A)
- scoreboard update, record-once, reset
- computer move selection (win, block, prefer-win-over-block, center, corner, full board)
- move after game completion, wrong-player rejection

There are no frontend tests in this submission — see "Future improvements".

## Project structure

```
tictactoe/
├── backend/                          ASP.NET Core 8 minimal API
│   ├── Backend.csproj
│   ├── Program.cs                    WebApplication setup + endpoint mapping
│   ├── Game.cs                       Game state, move/undo/reset, win detection
│   ├── AiPlayer.cs                   Computer move logic (heuristic priority)
│   ├── Scoreboard.cs                 Singleton X/O/draws counter, record-once
│   └── Requests.cs                   NewGameRequest, MoveRequest records
├── backend.tests/                    xUnit unit tests (31 tests)
│   ├── backend.tests.csproj
│   ├── GameTests.cs                  rules, undo, validation, reset
│   ├── AiPlayerTests.cs              priority order, edge cases
│   └── ScoreboardTests.cs            record, dedupe, reset
├── frontend/                         Angular 17 standalone app
│   ├── angular.json
│   ├── package.json
│   ├── tsconfig.json / tsconfig.app.json
│   └── src/
│       ├── index.html
│       ├── main.ts                   bootstrap + HttpClient provider
│       ├── styles.css                global styles (body bg, font)
│       └── app/
│           ├── app.component.ts      single component, REST calls, state
│           ├── app.component.html    template
│           └── app.component.css     component styles
├── start.ps1                         setup + run backend & frontend
├── test.ps1                          runs dotnet test
├── make-zip.ps1                      builds a clean shareable zip (excludes node_modules etc.)
└── README.md                         this file
```

## How the code works

This section walks through how each feature is implemented end-to-end. Useful for understanding the codebase and for explaining it during review.

### Lifecycle of a move (data flow)

1. **User clicks a cell** in the Angular board.
2. The component calls `makeMove(i)` which `POST`s `{ index: i, player: currentPlayer }` to `/api/games/{id}/moves`.
3. The endpoint looks up the `Game` in a `ConcurrentDictionary` keyed by id, validates the request body (`index` 0..8 present, `player` is `"X"` or `"O"`), and then takes the game's `SyncRoot` lock for the rest of the operation.
4. `Game.TryMove` validates the move:
   - game must be `InProgress`
   - `index` must be 0..8
   - `player` must be `"X"` or `"O"`
   - cell must be empty
   - `player` must match `CurrentPlayer`
5. If valid, the cell is set, a `MoveRecord` is appended to `Moves`, then `RecomputeStatus` runs win/draw detection. Turn switches if the game is still `InProgress`.
6. If the game just ended, `Scoreboard.Record(game)` is called. The scoreboard uses a `HashSet<string>` of game ids to make sure each completed game is recorded **exactly once**.
7. In **AI mode**, if the game is still `InProgress` and it is O's turn, `AiPlayer.PickMove` runs and `TryMove` is called again with the computer's choice. If the AI move ends the game, `Scoreboard.Record` is called again (still deduped).
8. The endpoint returns the full `GameStateDto`, which the frontend sets as its state. Angular re-renders the board, status line, scoreboard, and move history from this single response.

### How the AI picks a move (`AiPlayer.PickMove`)

Exact priority required by the spec:

1. **Win** — if O has a move that completes a line, take it.
2. **Block** — otherwise, if X has a move that would win next, block it.
3. **Center** — otherwise, take cell 4 if free.
4. **Corner** — otherwise, take a random free corner (0, 2, 6, 8).
5. **Side** — otherwise, take a random free side (1, 3, 5, 7).

Step 1 and 2 share a helper `FindWinningMove(board, player)` that simulates each empty cell and checks `CheckWinner`. This is O(9) per call so simulation cost is negligible.

The heuristic is not unbeatable. A perfect player can sometimes force a draw against it. This was a deliberate trade-off (see "Design decisions" and the spec which only requires this priority list).

### How undo works

`Game.TryUndo` enforces **Option A** — if `Status != InProgress`, undo returns an error. Otherwise:

- **PvP mode**: pop the last `MoveRecord`, clear that cell on the board.
- **AI mode**: pop the last move and, if it was O (the computer), also pop the X before it. This matches the spec ("the computer move and the previous human move are removed together").

After mutating, `CurrentPlayer` is recomputed from `Moves.Count % 2` (even count → X's turn) and `RecomputeStatus` runs again as a safety net.

Because Option A blocks undo after game over, the scoreboard never has to roll back — it stays consistent.

### Scoreboard, concurrency and dedupe

- `Scoreboard` is registered as a singleton in DI, so all games share one session-level board.
- It uses a private `object _lock` and `lock { ... }` for every read and write — cheap and sufficient for local play.
- `Record(Game g)` uses `HashSet<string>.Add(g.Id)`. `Add` returns `false` if the id is already in the set, so duplicate calls (e.g. the AI-move branch re-checks status and records again) are no-ops.
- `Forget(string id)` removes a single id from the dedupe set. The `/reset` endpoint calls it before clearing the board so a replayed game can be counted on its next completion.
- The `games` dictionary is a `ConcurrentDictionary<string, Game>`, which protects dictionary access. ASP.NET Core can dispatch two requests for the same game id concurrently, so each `Game` also owns a `SyncRoot` object; every endpoint that touches game state takes `lock (g.SyncRoot)` for the whole operation (move + AI auto-response + scoreboard recording + DTO snapshot). This makes a single request atomic against that game and prevents board corruption, lost scoreboard updates, or `List<MoveRecord>` enumeration errors under concurrent calls.

### Frontend rendering

`AppComponent` holds these pieces of state:
- `game: GameState | null` — the latest server response for the current game
- `scoreboard: ScoreDto` — the latest scoreboard (also included on every game response, but tracked separately so the score panel keeps showing the right number when there is no active game)
- `busy: boolean` — set to `true` while any game-mutating request is in flight; bound to `[disabled]` on cells, mode dropdown, and action buttons so the user cannot fire a second request before the first returns
- `sessionVersion: number` — bumped on New Game, Reset Game, and mode change; every request captures the version at issue time and a late response is dropped if it does not match, so a slow `/moves` reply cannot overwrite a fresh session
- `errorMsg: string | null` — inline error banner that shows the backend's structured `error` field for 4xx responses, or a network message for transport errors (replaces the old `alert()`)

`status()`, `canUndo()`, and `isWinningCell(i)` are pure functions over that state. The template binds:
- `[disabled]="busy || !!cell || game.status !== 'InProgress'"` on each cell.
- `[disabled]="busy || !canUndo()"` on the Undo button.
- `[class.win]="isWinningCell(i)"` on each cell — green highlight comes from `.cell.win` in the component CSS.

The move-history table iterates `game.moves` and renders `Row {{ m.row }}, Column {{ m.col }}` per the spec's example.

## API contract

All endpoints return JSON. Base URL: `http://localhost:5050`. CORS is open for `http://localhost:4200`.

### `POST /api/games` — create a new game session

Request:
```json
{ "mode": "pvp" }
```
`mode` may be `"pvp"` or `"ai"`. If omitted or invalid, defaults to `"pvp"`.

Response `200 OK`:
```json
{
  "id": "9d8dcadc",
  "mode": "pvp",
  "board": ["", "", "", "", "", "", "", "", ""],
  "currentPlayer": "X",
  "status": "InProgress",
  "winner": null,
  "winningCells": null,
  "moves": [],
  "scoreboard": { "x": 0, "o": 0, "draws": 0 }
}
```

### `GET /api/games/{id}` — get current state

Response `200 OK`: same `GameStateDto` shape as above.
Response `404 Not Found` if the id is unknown.

### `POST /api/games/{id}/moves` — submit a move

Request:
```json
{ "index": 4, "player": "X" }
```
`index` is **required** and must be 0–8 (top-left to bottom-right, row-major). `player` is **required** and must be `"X"` or `"O"` (case-sensitive). Missing or invalid fields produce `400 Bad Request`.

Response `200 OK` (after three moves; in AI mode the computer response is already applied):
```json
{
  "id": "9d8dcadc",
  "mode": "pvp",
  "board": ["X", "X", "", "", "O", "", "", "", ""],
  "currentPlayer": "O",
  "status": "InProgress",
  "winner": null,
  "winningCells": null,
  "moves": [
    { "moveNumber": 1, "player": "X", "row": 1, "col": 1, "index": 0 },
    { "moveNumber": 2, "player": "O", "row": 2, "col": 2, "index": 4 },
    { "moveNumber": 3, "player": "X", "row": 1, "col": 2, "index": 1 }
  ],
  "scoreboard": { "x": 0, "o": 0, "draws": 0 }
}
```

Response on a winning move includes `status: "Won"`, `winner: "X"`, and `winningCells: [0, 1, 2]` (for example).

Response `400 Bad Request` examples:
```json
{ "error": "Cell is already taken" }
```
```json
{ "error": "It is not O's turn" }
```
```json
{ "error": "Game is already over" }
```
```json
{ "error": "Cell index out of range" }
```
```json
{ "error": "Player must be X or O" }
```

### `POST /api/games/{id}/undo` — undo last move

No request body.

Response `200 OK`: updated `GameStateDto`.
Response `400 Bad Request`:
```json
{ "error": "No moves to undo" }
```
```json
{ "error": "Cannot undo after the game is over" }
```

### `POST /api/games/{id}/reset` — reset the current game

No request body. Clears board, history, winner, and sets `currentPlayer` back to `"X"`. **Scoreboard is unchanged.**

Response `200 OK`: fresh `GameStateDto` with the same `id` and `mode`.

### `GET /api/scoreboard`

Response `200 OK`:
```json
{ "x": 0, "o": 0, "draws": 0 }
```

### `POST /api/scoreboard/reset`

Zeroes all three counters. Returns the new scoreboard:
```json
{ "x": 0, "o": 0, "draws": 0 }
```

### Game status values

`status` is always one of:
- `"InProgress"` — game is still being played
- `"Won"` — someone won (`winner` and `winningCells` are populated)
- `"Draw"` — board is full with no winner

## Design decisions

### Clarification 2 — Undo + Scoreboard

The spec offers two options:
- **Option A**: disable Undo after a game is completed; scoreboard stays final.
- **Option B**: allow Undo even after completion; if the result is reversed, adjust the scoreboard accordingly.

**Chosen: Option A.** Reasoning:
- The scoreboard has a clean "record-once per game id" invariant. Option B would require the scoreboard to support decrement, which complicates that invariant and creates failure modes (what if you undo to before a previous win, replay, and reach a different winner?).
- Reset Game already provides a natural way to start fresh while keeping the scoreboard.
- Matches the typical UX of physical Tic Tac Toe.

### Backend is the single source of truth

The Angular component holds zero game logic. Every action posts to the backend and renders the returned `GameStateDto`. This:
- Prevents the UI from getting out of sync with the rules
- Makes the backend independently testable (which is why we have 31 xUnit tests and zero frontend tests in this iteration)
- Means a different client (CLI, another frontend) could plug in without re-implementing rules

### Scoreboard as a DI singleton

Registered with `builder.Services.AddSingleton<Scoreboard>()`. It's session-level (per spec) and shared across all games in the process. Internal `lock` protects against concurrent updates from multiple game requests landing at once.

### Including the scoreboard in every game response

Every `GameStateDto` carries the current `scoreboard`. The frontend could call `GET /api/scoreboard` separately, but bundling it saves a round-trip after every action.

### Splitting backend into small files

`Program.cs` is intentionally thin — just the WebApplication setup and `MapPost` / `MapGet` calls. Game rules live in `Game.cs`, AI in `AiPlayer.cs`, scoreboard in `Scoreboard.cs`. Each class has one clear responsibility and is easy to unit-test in isolation.

### Heuristic AI (not minimax)

The spec's priority list (win → block → center → corner → any) is exactly what's implemented. Minimax would be unbeatable but the spec didn't ask for it, and a beatable AI feels more playful for a 2-player game on a friend's laptop. Easy to swap in later if needed.

### Move history shape

The backend stores raw indices internally but exposes 1-indexed `row` and `col` in the DTO, so the frontend can render `"Row 1, Column 1"` without computing it. The raw `index` is still included for clients that want it.

### Computer move applied in the same request

When the human posts a move in AI mode, the backend immediately computes and applies the AI response inside the same handler — under the game's `SyncRoot` lock — before returning. This gives the frontend a single DTO that already contains both moves, so:
- There is no "computer is thinking" round-trip race.
- The board, status, history and scoreboard all advance atomically.
- The frontend additionally sets a `busy` flag for the duration of the request, which disables the cells and the action buttons. So even though the request is asynchronous, the user cannot place a second X before the response returns.

### Reset Game vs New Game (and the scoreboard interaction)

These look similar but mean different things in this codebase:
- **New Game** (`POST /api/games`) creates a fresh game session with a brand-new id. Used when starting from scratch or after a mode switch.
- **Reset Game** (`POST /api/games/{id}/reset`) clears the *current* game's board, status, and history but **keeps the same game id and the same mode**. Useful for abandoning a position mid-play without leaving the mode selector.

Because Reset Game preserves the game id and the scoreboard records each completed game id at most once, the scoreboard endpoint explicitly calls `Scoreboard.Forget(id)` before resetting so the next completion of that same session can still be counted. Without that call, replaying after a Reset would silently fail to score (a unit test, `Forget_AllowsSameGameIdToBeCountedAgain`, locks this behavior down).

### Mode switch starts a clean session

When the user changes the dropdown from "2 Players" to "vs Computer" or back, the frontend (a) discards the in-progress game and (b) calls `POST /api/scoreboard/reset` before starting a new game in the chosen mode. Reason: an X win against a human and an X win against the computer are not comparable, so aggregating them in a single counter would be misleading. The clean break also matches what most users expect when they explicitly switch game modes.

## Clarifications and assumptions

- "Session-level scoreboard" is interpreted as **per backend process** — it resets when the server restarts. The spec allowed in-memory storage explicitly.
- In AI mode, **human is always X, computer is always O** (per spec).
- The `player` field in a move request is treated as a sanity check the backend validates against `CurrentPlayer`. The Angular client always sends the correct value; the check defends against a buggy or multi-tab client.
- New Game and Reset Game both keep the scoreboard. The only way to zero the scoreboard is **Reset Scoreboard**.
- Endpoint paths use the spec's plural form (`/games`, `/moves`). The exact paths in the spec table were taken as the contract.
- The browser auto-opens at `http://localhost:4200`; if the dev server isn't ready yet, refresh.

## Known limitations

- No game or scoreboard persistence — backend restart loses everything.
- No authentication. Assumes a single local user.
- No frontend unit tests. Only backend logic is covered.
- AI is heuristic, not minimax — a skilled player can sometimes force a draw.
- No graceful inter-window shutdown — closing the launcher window does not stop the two server windows. Use the `Stop-Process` line that `start.ps1` prints, or close each window.
- Errors from the backend currently surface as a browser `alert("Could not reach server...")`. An in-app banner would be friendlier.
- The launch script is Windows-only (`.ps1` + `Start-Process` with `powershell.exe`). Linux/macOS would need a `start.sh` equivalent.

## Future improvements

- Persistence: swap `ConcurrentDictionary<string, Game>` and the in-memory scoreboard for SQLite or any IRepository — both classes already isolate their state behind methods.
- Hard mode: a minimax `AiPlayer` plugged in via an interface.
- Choose-your-symbol and choose-who-starts in AI mode.
- Frontend tests (Karma/Jasmine) covering board rendering, status messages, and HTTP integration with `HttpTestingController`.
- WebSocket push for spectator mode / network multiplayer.
- Inline error banner instead of `alert()`.
- A cross-platform `start.sh` and a Docker compose file.

## AI-assisted development notes

This project was built with the help of GitHub Copilot CLI. The spec explicitly asks for honesty about the AI workflow, so here is what actually happened, broken down by the seven points the spec lists.

### 1. How the requirement was converted into a specification

I did not jump straight into code. Before any file was created I spent time writing down what "tic tac toe" actually needed to mean for this project, and made a few architecture calls up front so the build had a target to hit.

The thinking went roughly like this:

- **Scope of "the game itself."** A 3x3 grid, two players, win/draw detection, end-of-game banner. That is the floor — anything below it is not a tic tac toe game.
- **What "simple" means here.** The brief said "no overengineering" and "make it look like a human built it in a day". I read that as: no UI framework (no Material, no Tailwind), no state-management library, no minimax, no database, no JWT, no Docker. Plain CSS, in-memory storage, one Angular component, a handful of REST endpoints.
- **Where the rules should live.** I deliberately decided the backend would own *all* the game rules — win detection, turn switching, AI move selection, scoreboard. The frontend would be a thin renderer that calls REST and re-renders whatever the server sends back. That choice removed a whole class of "client and server disagree" bugs and meant I could write unit tests in C# instead of writing E2E tests in the browser.
- **Game modes.** I went back and forth on this. Player-vs-player only would have been faster, but the spec doc would later turn out to ask for an AI mode anyway, and even before the doc arrived I felt a tic tac toe submission with no AI looked thin. Decision: support both modes, switchable at the "new game" step.
- **AI strength.** Full minimax would solve the game completely and feel like playing a wall. The spec's intent (later confirmed) was a "smart but beatable" opponent. I picked a four-tier heuristic — take a win → block a loss → take center → corner → side — which is well known to feel like a real opponent without being unbeatable.
- **Storage.** SQLite would have added a migration story for something with no real persistence requirement. In-memory `ConcurrentDictionary` keeps the build a single binary, makes tests trivial, and the cost is one line in "Known limitations".
- **Setup.** "Single command on a fresh machine" was a hard requirement. I went with a `.ps1` launcher that opens two windows so each server's logs stay readable, rather than one window with interleaved output.

After that block of thinking, I had a one-page mental spec: stack, endpoints, DTO shape, AI policy, undo policy, scoreboard policy. That is what I started building against.

Later in the process the formal `Problem_Statement.docx` was added to the folder. I went through it line by line and produced a gap analysis — each requirement marked as already covered, partial, or missing — and the missing items (move history with row/column rendering, undo, dedicated reset endpoints, plural endpoint naming, status enum, wrong-player validation, xUnit project) became the second-pass to-do list.

### 2. Prompts used

The workflow was not "dump the spec, get a project back". It was incremental, one slice at a time, with manual review between each slice. Roughly chronologically:

1. *"Scaffold a minimal .NET 8 web project with a single Game class that holds a 3x3 board and a TryMove method. Just the class — no endpoints, no AI, no scoreboard. I want to write the win-detection myself."* This was the seed; everything was added on top of it.
2. *"Generate the eight winning lines as an `int[][]` table so I can iterate them in a single loop."* I had the algorithm in mind; I just did not want to hand-type the index combinations.
3. *"Wire up minimal REST endpoints for create game, get game, and submit move. Use minimal APIs, not controllers."* I reviewed the routes and renamed them to plural (`/games`, `/moves`) myself.
4. *"Write an Angular 17 standalone component that posts to these endpoints, no extra libraries."* I rewrote the template by hand because the first draft used Material-style classes I did not want.
5. *"Generate a heuristic AI with this exact priority order: win, block, center, corner, side. Each tier should be its own method so I can unit test them individually."* I supplied the policy; the AI wrote the loops.
6. *"Add an in-memory Scoreboard singleton. Snapshot returns X, O, Draws. Lock on every read and write. Dedupe completed games by id."* I designed the dedupe approach; the AI wrote the lock plumbing.
7. *"Write unit tests for: X wins top row, O wins diagonal, draw with full board, move on occupied cell rejected, wrong-player rejected, win returns winning line, scoreboard counted exactly once per game."* The test names are mine; the assertion bodies came back as drafts I edited.
8. *"Add an undo endpoint. Policy: disabled after game-over. In PvP mode pop one move; in AI mode pop two so the human's turn is restored."* I designed the policy; the AI implemented the list operations.
9. *"Add a move history to the game DTO: move number, player, row 1-3, column 1-3, raw index."* The row/column shape is a manual decision (1-indexed for humans) and I corrected the draft which had used 0-indexed values.
10. After the spec doc arrived, individual targeted prompts per missing item rather than one big "make it spec-compliant" prompt — for example *"the spec wants an explicit status enum on the DTO, refactor the existing booleans into `InProgress` / `Won` / `Draw`"*.
11. Debugging prompts when something broke: *"localhost:4200 is empty but the cmd window is open — figure out why ng serve is not binding"* led to a multi-step investigation that ended with the `cmd /k` → `powershell -NoExit` fix.
12. README structure was sketched by me in bullet form first; the prose was iterated on.

Throughout, I rejected drafts that did too much (a draft `Program.cs` once had three middlewares I did not want), and asked for narrower regenerations.

### 3. What the AI generated

The AI was most useful for things that are tedious to type but easy to verify:

- Project plumbing files I always look up: `Backend.csproj`, `angular.json`, `tsconfig.json`, `tsconfig.app.json`, the standalone-component `main.ts` bootstrap.
- The first-draft skeleton of `Game.cs` and `Program.cs` after I described the shape.
- Arrange-Act-Assert scaffolding for the unit tests — I wrote the list of test names and what each should assert, the AI filled in the boilerplate.
- The eight winning-lines `int[][]` table.
- The first pass of `app.component.html` and the matching `.css` baseline.
- The initial `start.ps1`, `test.ps1`, `make-zip.ps1` drafts (later all three needed manual fixes).
- First draft of this README's section headers and tables.

### 4. What was changed manually after generation

The list is long because almost every AI draft was edited rather than committed as-is.

Architecture and design decisions (made by me, before or instead of prompting):
- Backend-thick / frontend-thin split, with the server as the only source of truth for game state.
- The `GameStatus` enum and the decision to replace earlier `IsGameOver` / `IsDraw` booleans with a single state field.
- The DTO shape returned to the frontend (move history with `row`, `col`, `index`; winning cells as an `int[]`; scoreboard embedded so the UI does not need a second request after every move).
- The undo policy (Option A: disabled after game-over; AI mode pops two moves). Documented in "Design decisions" with the trade-off vs Option B.
- The scoreboard dedupe approach (track recorded game ids in a `HashSet` so re-recording the same game is a no-op).
- Splitting the backend into `Program.cs`, `Game.cs`, `AiPlayer.cs`, `Scoreboard.cs`, `Requests.cs` — the AI's first draft put everything in `Program.cs`. I refactored it.
- The Reset Game vs New Game vs Reset Scoreboard separation — three distinct endpoints with three different semantics, decided by me after re-reading the spec.

Code I rewrote or significantly edited:
- `app.component.html` and `.css` — rewrote the markup and most of the styles by hand to land on the plain look. The AI's first draft used gradient buttons and shadows.
- `Scoreboard.cs` — refactored `Snapshot()` from returning an anonymous `object` to returning a public `ScoreboardSnapshot` record after I discovered the `dynamic` tests in `ScoreboardTests` were silently failing because anonymous types are internal across assemblies. Spent some time isolating the root cause before fixing it.
- `start.ps1` — the first version launched both servers with `cmd /k npm start`. The Angular dev server silently failed to bind port 4200 in that environment (the cmd window stayed open, `node` was running, but nothing was listening). I reproduced standalone, confirmed `npx ng serve` worked directly, narrowed it to the `cmd /k` wrapper, and switched both launches to `powershell -NoExit -Command`. End-to-end debug-and-fix, not a regeneration.
- `backend.tests.csproj` — `dotnet new xunit` on this machine scaffolded a `net10.0` target framework. I noticed, pinned it to `net8.0` to match the backend, and re-ran.
- Tightened CORS from `AllowAnyOrigin` to a hard-coded `http://localhost:4200` because the loose version was unnecessary for a localhost-only dev setup.
- Added the wrong-player check in `Game.TryMove` after spotting that the AI draft only validated the cell, not the caller.
- Added explicit test cases the first-pass generation missed: undo after game-over rejected, scoreboard counted exactly once per game across multiple GETs, wrong-player rejected, win returns the actual winning cells (not just the winner symbol).
- Decision to keep `start.ps1` non-installing (clear error on missing SDK) rather than auto-installing via winget. An earlier AI draft had defaulted to a richer install path; I removed it.

README content I wrote by hand, not generated:
- The "Design decisions" section — every entry there is a personal judgment call.
- The "Clarifications and assumptions" section.
- This AI-assisted-development section.
- The "How the code works" walkthroughs (data flow, undo logic, scoreboard concurrency) — these describe choices I made, so they are mine.

Realistic split: the AI saved me typing time on plumbing, scaffolding, and repetitive test setup — call it 55-60% of the raw lines on disk. The architecture, the policies, the debugging, the security tightening, the test design, and most of this README came from me — and that is the part that actually decides whether the project is good.

### 5. Parts reviewed carefully

- **Win detection** — verified the 8 winning lines and that `CheckWinner` returns the actual cells (not just the winner) so the frontend can highlight them.
- **Undo correctness** — manually traced the AI-mode undo case (X plays, AI plays O, undo removes both, turn returns to X) and added a dedicated unit test.
- **Scoreboard dedupe** — verified that recording the same game twice does not double-count (test: `RecordsCompletedGame_OnlyOnce`).
- **Wrong-player rejection** — added a unit test plus an end-to-end REST check.
- **Concurrency** — added an explicit `lock` around all scoreboard reads and writes; used `ConcurrentDictionary` for the games map; documented the reasoning.
- **CORS** — restricted to the dev server origin only (`http://localhost:4200`), not `AllowAnyOrigin`.
- **Launcher bug** — root-caused why `ng serve` did not bind under `cmd /k` before fixing, rather than just changing things and hoping.

### 6. Assumptions

Documented in "Clarifications and assumptions" above. The main ones:
- Session-level scoreboard = per process, resets on backend restart.
- Human is X, computer is O (per spec — not negotiated).
- The `player` field in a move request is a sanity check, not strictly necessary because the backend already tracks turn.
- New Game and Reset Game both preserve the scoreboard; only Reset Scoreboard zeroes it.

### 7. Trade-offs chosen

- **Option A over Option B** for Clarification 2 — simpler scoreboard invariant, fewer edge cases (see Design decisions).
- **Heuristic AI over minimax** — matches the spec's priority list exactly; feels more like a real opponent than a wall. Easy to upgrade later.
- **In-memory over SQLite** — the spec allowed it and it removed a dependency. Acknowledged as a limitation; isolated behind methods so swapping in SQLite is straightforward.
- **Backend-thick, frontend-thin** — all rules live in C# so backend tests cover correctness without needing E2E browser tests. Trade-off: a small UI change requires no backend deploy, but a rules change requires no frontend deploy either.
- **Computer auto-responds inside the same request** — simpler client, one round trip, no "thinking" race. Trade-off: a slow AI (if upgraded to minimax with full search) would visibly delay the response; in practice the heuristic is instant.
- **Three separate PowerShell windows** (launcher + backend + frontend) instead of one console with interleaved output — easier to read each server's log; trade-off is closing them is a manual step.
- **Plain CSS, no UI library** — keeps the bundle small (~140 KB main.js) and avoids a "ChatGPT-generated landing page" look. Trade-off: no design polish.
