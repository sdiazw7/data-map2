namespace DataMap.Api.DTOs;

/// <summary>A column's version after an edit, which becomes the token for the caller's next one.</summary>
public record ColumnVersionDto(Guid ColumnId, int Version);

/// <summary>
/// The result of a bulk edit. Every accepted row's Version is incremented server-side, and the
/// client needs the new value to make its next optimistic write — returning nothing forced a
/// full grid refetch after each edit just to recover numbers the server already had.
/// </summary>
public record BulkUpdateResponse(IReadOnlyList<ColumnVersionDto> Columns);
