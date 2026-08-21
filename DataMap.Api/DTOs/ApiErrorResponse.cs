namespace DataMap.Api.DTOs;

/// <param name="Code">Stable, machine-readable identifier. Clients branch on this, not on the message.</param>
/// <param name="Message">Human-readable text, safe to show a user.</param>
public record ApiError(string Code, string Message);

/// <summary>
/// The single error shape for the whole API. Every failure — thrown, unauthenticated, or
/// produced by the framework before a handler ran — is serialized through this.
/// </summary>
public record ApiErrorResponse(ApiError Error);
