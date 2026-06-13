namespace Backend;

public record NewGameRequest(string? Mode);
// Index is nullable so we can detect missing values; an empty body would otherwise
// silently bind Index = 0 and play cell 0.
public record MoveRequest(int? Index, string? Player);
