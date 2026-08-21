namespace DataMap.Api.DTOs;

/// <summary>A column's version after an edit, which becomes the token for the caller's next one.</summary>
public record ColumnVersionDto(Guid ColumnId, int Version);

/// <summary>
/// A row the server declined because the caller's version was stale, carrying the version it
/// holds now. The client rolls that one cell back and leaves the rest of the batch alone.
/// </summary>
public record ColumnConflictDto(Guid ColumnId, int CurrentVersion);

/// <summary>
/// The result of a bulk edit. Every accepted row's Version is incremented server-side, and the
/// client needs the new value to make its next optimistic write — returning nothing forced a
/// full grid refetch after each edit just to recover numbers the server already had.
///
/// A stale row is reported in <see cref="Conflicts"/> rather than failing the request: one cell
/// that moved under the user must not discard the other 499 cells of a pasted range.
/// </summary>
public record BulkUpdateResponse(
    IReadOnlyList<ColumnVersionDto> Columns,
    IReadOnlyList<ColumnConflictDto> Conflicts);
