using DataMap.Api.Exceptions;

namespace DataMap.Api.Endpoints;

/// <summary>
/// Typed access to the identity <see cref="Middleware.SessionAuthMiddleware"/> puts on the
/// request. Endpoints used to unbox these by hand, which turned a missing item — an endpoint
/// mapped outside the authenticated set — into a NullReferenceException and a 500. Reading
/// them through here reports the real problem instead.
/// </summary>
public static class RequestContext
{
    public const string WorkspaceIdKey = "WorkspaceId";
    public const string ParticipantIdKey = "ParticipantId";

    public static Guid WorkspaceId(this HttpContext context) => Read(context, WorkspaceIdKey);

    public static Guid ParticipantId(this HttpContext context) => Read(context, ParticipantIdKey);

    private static Guid Read(HttpContext context, string key)
    {
        if (context.Items.TryGetValue(key, out var value) && value is Guid id)
            return id;

        throw new UnauthorizedException();
    }
}
