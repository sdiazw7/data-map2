using DataMap.Api.Exceptions;
using Microsoft.Extensions.Logging;

namespace DataMap.Api.Services;

public abstract class BaseService(ILogger logger)
{
    protected readonly ILogger Logger = logger;

    /// <summary>Rejects the request with a 400 unless the condition holds.</summary>
    protected static void Require(bool condition, string message)
    {
        if (!condition) throw new ValidationException(message);
    }

    /// <summary>
    /// Validates a required free-text field and returns it trimmed. Trimming here rather than at
    /// each call site keeps stored values consistent no matter which service took them in.
    /// </summary>
    protected static string RequireText(string? value, string field, int maxLength)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        Require(trimmed.Length > 0, $"{field} is required.");
        Require(trimmed.Length <= maxLength, $"{field} is longer than the {maxLength:N0} character limit.");
        return trimmed;
    }

    /// <summary>Validates an optional free-text field, returning it trimmed, or null when blank.</summary>
    protected static string? OptionalText(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var trimmed = value.Trim();
        Require(trimmed.Length <= maxLength, $"{field} is longer than the {maxLength:N0} character limit.");
        return trimmed;
    }
}
