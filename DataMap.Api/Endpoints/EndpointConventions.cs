using DataMap.Api.DTOs;

namespace DataMap.Api.Endpoints;

public static class EndpointConventions
{
    /// <summary>
    /// Declares the error statuses a route can produce, all carrying <see cref="ApiErrorResponse"/>.
    /// Without this the generated contract claims the success shape is the only one, and the
    /// hand-written client types are the only record that an error body exists at all.
    /// </summary>
    public static RouteHandlerBuilder ProducesApiErrors(this RouteHandlerBuilder builder, params int[] statusCodes)
    {
        foreach (var statusCode in statusCodes)
            builder.Produces<ApiErrorResponse>(statusCode);

        return builder;
    }

    /// <summary>The error statuses every authenticated route shares.</summary>
    public static RouteHandlerBuilder ProducesAuthErrors(this RouteHandlerBuilder builder)
        => builder.ProducesApiErrors(StatusCodes.Status401Unauthorized);
}
