import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';

interface MoveDto {
  moveNumber: number;
  player: string;
  row: number;
  col: number;
  index: number;
}

interface ScoreDto { x: number; o: number; draws: number; }

interface GameState {
  id: string;
  mode: string;
  board: string[];
  currentPlayer: string;
  status: 'InProgress' | 'Won' | 'Draw';
  winner: string | null;
  winningCells: number[] | null;
  moves: MoveDto[];
  scoreboard: ScoreDto;
}

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css']
})
export class AppComponent {
  apiBase = 'http://localhost:5050/api';
  game: GameState | null = null;
  mode: 'pvp' | 'ai' = 'pvp';
  loading = false;
  busy = false;                 // true while any game-mutating request is in flight
  errorMsg: string | null = null;
  scoreboard: ScoreDto = { x: 0, o: 0, draws: 0 };

  // Bumped on every action that starts a fresh session (new game, reset game,
  // mode change). Stale HTTP responses from a previous session are discarded
  // by comparing the captured version against the current one.
  private sessionVersion = 0;

  constructor(private http: HttpClient) {
    this.refreshScoreboard();
  }

  newGame() {
    if (this.busy) return;
    this.busy = true;
    this.loading = true;
    this.errorMsg = null;
    const v = ++this.sessionVersion;
    this.http.post<GameState>(`${this.apiBase}/games`, { mode: this.mode })
      .subscribe({
        next: g => {
          this.busy = false; this.loading = false;
          if (v !== this.sessionVersion) return;
          this.game = g; this.scoreboard = g.scoreboard;
        },
        error: err => { this.busy = false; this.loading = false; this.handleError(err, v); }
      });
  }

  makeMove(i: number) {
    if (this.busy) return;
    if (!this.game || this.game.status !== 'InProgress' || this.game.board[i]) return;
    this.busy = true;
    this.errorMsg = null;
    const v = this.sessionVersion;
    const targetId = this.game.id;
    this.http.post<GameState>(`${this.apiBase}/games/${targetId}/moves`,
        { index: i, player: this.game.currentPlayer })
      .subscribe({
        next: g => {
          this.busy = false;
          if (v !== this.sessionVersion) return;
          this.game = g; this.scoreboard = g.scoreboard;
        },
        error: err => { this.busy = false; this.handleError(err, v); }
      });
  }

  undoMove() {
    if (this.busy || !this.canUndo()) return;
    this.busy = true;
    this.errorMsg = null;
    const v = this.sessionVersion;
    this.http.post<GameState>(`${this.apiBase}/games/${this.game!.id}/undo`, {})
      .subscribe({
        next: g => {
          this.busy = false;
          if (v !== this.sessionVersion) return;
          this.game = g; this.scoreboard = g.scoreboard;
        },
        error: err => { this.busy = false; this.handleError(err, v); }
      });
  }

  resetGame() {
    if (this.busy || !this.game) return;
    this.busy = true;
    this.errorMsg = null;
    const v = ++this.sessionVersion;   // treat Reset Game as a new session for stale-response purposes
    this.http.post<GameState>(`${this.apiBase}/games/${this.game.id}/reset`, {})
      .subscribe({
        next: g => {
          this.busy = false;
          if (v !== this.sessionVersion) return;
          this.game = g; this.scoreboard = g.scoreboard;
        },
        error: err => { this.busy = false; this.handleError(err, v); }
      });
  }

  resetScoreboard() {
    if (this.busy) return;
    this.errorMsg = null;
    this.http.post<ScoreDto>(`${this.apiBase}/scoreboard/reset`, {})
      .subscribe({
        next: s => this.scoreboard = s,
        error: err => this.handleError(err, this.sessionVersion)
      });
  }

  // When the user switches PvP <-> AI the running game and scoreboard
  // both belong to the previous mode, so start a clean session in the new mode.
  onModeChange(newMode: 'pvp' | 'ai') {
    if (newMode === this.mode || this.busy) return;
    this.mode = newMode;
    this.game = null;
    this.errorMsg = null;
    this.sessionVersion++;   // discard any in-flight responses from the old session
    this.http.post<ScoreDto>(`${this.apiBase}/scoreboard/reset`, {})
      .subscribe({
        next: s => { this.scoreboard = s; this.newGame(); },
        error: err => this.handleError(err, this.sessionVersion)
      });
  }

  canUndo(): boolean {
    return !!this.game
        && this.game.status === 'InProgress'
        && this.game.moves.length > 0;
  }

  isWinningCell(i: number): boolean {
    return !!this.game?.winningCells?.includes(i);
  }

  status(): string {
    if (!this.game) return '';
    if (this.game.status === 'Won') return `${this.game.winner} wins!`;
    if (this.game.status === 'Draw') return `It's a draw.`;
    return `${this.game.currentPlayer}'s turn`;
  }

  dismissError() { this.errorMsg = null; }

  private refreshScoreboard() {
    this.http.get<ScoreDto>(`${this.apiBase}/scoreboard`)
      .subscribe({
        next: s => this.scoreboard = s,
        error: () => { /* backend may not be up yet; ignore */ }
      });
  }

  private handleError(err: HttpErrorResponse, capturedVersion: number) {
    if (capturedVersion !== this.sessionVersion) return;   // stale, ignore silently
    if (err.status === 0) {
      this.errorMsg = 'Could not reach the server. Is the backend running on port 5050?';
    } else if (err.status >= 400 && err.status < 500 && err.error?.error) {
      this.errorMsg = err.error.error;   // backend's structured message (e.g., "Cell is already taken")
    } else if (err.status === 404) {
      this.errorMsg = 'Game not found. Start a New Game.';
    } else {
      this.errorMsg = `Unexpected server error (HTTP ${err.status}).`;
    }
  }
}
