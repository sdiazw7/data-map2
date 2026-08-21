using DataMap.Api.DTOs;

namespace DataMap.Api.Services;

/// <summary>
/// What a successful join produces: the response body, and the session the endpoint should
/// issue a cookie for. The session id is deliberately kept out of <see cref="JoinResponse"/> —
/// it is a credential, and it belongs in an HttpOnly cookie rather than a JSON payload.
/// </summary>
public record JoinResult(JoinResponse Response, Guid SessionId);
