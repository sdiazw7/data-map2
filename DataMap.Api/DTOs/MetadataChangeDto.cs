namespace DataMap.Api.DTOs;

/// <summary>
/// One recorded edit to a column, as the history panel shows it. Values are the ones that were
/// written, not ids, so an entry reads on its own without resolving anything against the
/// catalog as it stands now.
/// </summary>
/// <param name="Field">The column property that changed, e.g. "Description" or "BusinessTerm".</param>
/// <param name="OldValue">What it held before. Null where it held nothing.</param>
/// <param name="NewValue">What it was set to. Null where the edit cleared it.</param>
/// <param name="EditedByEmail">
/// The participant who made the edit. Invite-based access has no display names, so the email
/// is the only identity there is to show.
/// </param>
public record MetadataChangeDto(
    Guid Id,
    string Field,
    string? OldValue,
    string? NewValue,
    string EditedByEmail,
    DateTime EditedAt);
