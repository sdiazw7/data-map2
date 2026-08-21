namespace DataMap.Api.Endpoints;

/// <summary>
/// Issues the participant session cookie. Writing it is a transport concern, so it lives at the
/// endpoint layer — the services that establish a session return its id and stay HTTP-free.
/// </summary>
public static class SessionCookie
{
    public const string Name = "participant_session";

    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    public static void Issue(HttpContext context, Guid sessionId)
    {
        context.Response.Cookies.Append(Name, sessionId.ToString(), new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.Add(Lifetime),
        });
    }
}
