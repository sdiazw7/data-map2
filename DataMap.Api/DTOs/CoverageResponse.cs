namespace DataMap.Api.DTOs;

public record CoverageResponse(
    int TotalColumns,
    int DocumentedColumns,
    double CoveragePercent
);
