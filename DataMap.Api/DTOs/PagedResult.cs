namespace DataMap.Api.DTOs;

/// <summary>
/// The envelope every list endpoint returns. A bare JSON array cannot carry the total, so a
/// client has no way to tell a last page from a full one, and there is nowhere to add a field
/// later without breaking every caller.
/// </summary>
/// <param name="Items">The rows for this page.</param>
/// <param name="Total">Rows matching the query across all pages, ignoring limit and offset.</param>
/// <param name="Limit">The page size actually applied, which may be lower than the one asked for.</param>
/// <param name="Offset">The offset this page starts at.</param>
public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Limit, int Offset);
