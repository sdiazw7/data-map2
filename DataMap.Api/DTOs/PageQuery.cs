namespace DataMap.Api.DTOs;

/// <summary>
/// Paging arguments shared by the list endpoints. Defaults are applied at the endpoint, so a
/// caller that passes neither still gets a bounded page.
/// </summary>
public record PageQuery(int Limit, int Offset);
