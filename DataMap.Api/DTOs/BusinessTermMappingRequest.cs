namespace DataMap.Api.DTOs;

/// <summary>Body of a business-term assignment; the column is identified by the route.</summary>
public record BusinessTermMappingRequest(Guid TermId);
