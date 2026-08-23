using DataMap.Api.Data;
using DataMap.Api.DTOs;
using DataMap.Api.Exceptions;
using DataMap.Api.Models;
using DataMap.Api.Repositories;
using Microsoft.Extensions.Logging;

namespace DataMap.Api.Services;

/// <summary>
/// Serves and edits the metadata grid. Bulk CSV ingestion lives in
/// <see cref="MetadataImportService"/>.
/// </summary>
public class MetadataService(
    IColumnRepository columnRepo,
    IProjectionRepository projectionRepo,
    IMetadataChangeRepository changeRepo,
    IProjectionService projectionService,
    IUnitOfWork unitOfWork,
    ILogger<MetadataService> logger) : BaseService(logger), IMetadataService
{
    private static readonly HashSet<string> SortableColumns = ["columnName", "tableName", "dataType", "owner"];

    // Paging bounds. A workspace holds 100k+ columns, so an unbounded limit would read the
    // whole catalog into memory and defeat the point of paginating at all.
    private const int MaxLimit = 1000;

    // Table names are one row per distinct table, so the ceiling is lower than the grid's.
    private const int MaxTableNameLimit = 5_000;

    // History is read one column at a time in a panel, so a page is small by nature.
    private const int MaxHistoryLimit = 200;

    // Bulk edit bounds. The grid pastes in chunks; this caps one request, not one session.
    private const int MaxBulkUpdateRows = 5_000;
    private const int MaxDescriptionLength = 4_000;
    private const int MaxExampleValueLength = 1_000;
    private const int MaxOwnerLength = 200;

    public async Task<PagedResult<ColumnGridRow>> GetColumnsAsync(Guid workspaceId, MetadataColumnsQuery query)
    {
        Require(SortableColumns.Contains(query.SortBy),
            $"'{query.SortBy}' is not a sortable field. Allowed values: {string.Join(", ", SortableColumns)}.");
        Require(query.SortDir is "asc" or "desc", "sortDir must be 'asc' or 'desc'.");
        RequirePaging(query.Limit, query.Offset, MaxLimit);

        var (rows, total) = await projectionRepo.QueryAsync(
            workspaceId,
            query.Limit,
            query.Offset,
            query.Search,
            query.UndocumentedOnly,
            query.TableName,
            query.SortBy,
            query.SortDir);

        var items = rows.Select(r => new ColumnGridRow(
            r.ColumnId,
            r.SchemaName,
            r.TableName,
            r.ColumnName,
            r.DataType,
            r.ExampleValue,
            r.Description,
            r.BusinessTerm,
            r.Owner,
            r.Version
        )).ToList();

        return new PagedResult<ColumnGridRow>(items, total, query.Limit, query.Offset);
    }

    public async Task<BulkUpdateResponse> BulkUpdateAsync(Guid workspaceId, Guid participantId, List<ColumnUpdateRequest> updates)
    {
        ValidateUpdates(updates);

        // Read every target in one query, then decide row by row. The repositories each commit
        // on their own, so applying as the loop ran used to leave columns saved while their
        // audit records and projection sync were dropped on a later throw.
        var columns = await columnRepo.GetByIdsAsync(
            workspaceId,
            updates.Select(u => u.ColumnId).ToList());
        var columnsById = columns.ToDictionary(c => c.Id);

        var now = DateTime.UtcNow;
        var changes = new List<MetadataChange>();
        var edited = new List<Column>();
        var conflicts = new List<ColumnConflictDto>();

        foreach (var update in updates)
        {
            if (!columnsById.TryGetValue(update.ColumnId, out var column))
            {
                Logger.LogWarning(
                    "Column {ColumnId} not found in workspace {WorkspaceId} — skipping",
                    update.ColumnId, workspaceId);
                continue;
            }

            // A stale row is reported, not thrown. One cell that moved under the user must not
            // discard the rest of a pasted range. Nothing is applied to it, so the client can
            // roll back that cell alone and leave the others showing what was saved.
            if (column.Version != update.Version)
            {
                conflicts.Add(new ColumnConflictDto(column.Id, column.Version));
                continue;
            }

            changes.AddRange(ApplyUpdate(column, update, participantId, now));
            edited.Add(column);
        }

        // Nothing survived the version check, so there is no work to open a transaction for.
        if (edited.Count > 0)
        {
            await unitOfWork.ExecuteAsync(async () =>
            {
                // The Version check above is a read-then-write, so it cannot see a writer that
                // commits in between. Version is also an EF concurrency token, which puts the
                // value that was read into the UPDATE's WHERE clause and makes the database
                // reject the losing write instead of silently overwriting the winner. That
                // rejection cannot say which row lost, so the batch fails whole rather than
                // reporting a conflict it would have to guess at.
                if (!await columnRepo.UpdateRangeAsync(edited))
                    throw new VersionConflictException();

                if (changes.Count > 0)
                    await changeRepo.AddRangeAsync(changes);

                // Sync only the rows that changed. Rebuilding the whole projection here would
                // cost a full delete + reinsert of the workspace on every keystroke-level edit.
                await projectionService.SyncColumnsAsync(workspaceId, edited);
            });
        }

        if (conflicts.Count > 0)
        {
            Logger.LogInformation(
                "Bulk update in workspace {WorkspaceId} applied {AppliedCount} columns and declined {ConflictCount} as stale",
                workspaceId, edited.Count, conflicts.Count);
        }

        // Read after the commit: these are the versions a caller must send with its next edit.
        return new BulkUpdateResponse(
            edited.Select(c => new ColumnVersionDto(c.Id, c.Version)).ToList(),
            conflicts);
    }

    public async Task<CoverageResponse> GetCoverageAsync(Guid workspaceId)
    {
        var (total, documented) = await projectionRepo.GetCoverageCountsAsync(workspaceId);
        var percent = total == 0 ? 0.0 : Math.Round((double)documented / total * 100, 1);
        return new CoverageResponse(total, documented, percent);
    }

    public async Task<PagedResult<MetadataChangeDto>> GetColumnHistoryAsync(
        Guid workspaceId, Guid columnId, PageQuery page)
    {
        RequirePaging(page.Limit, page.Offset, MaxHistoryLimit);

        // A change record carries no workspace of its own, so the column is what scopes the
        // query. Without this check a participant could read another workspace's edit history,
        // including the values and the email of whoever wrote them.
        var column = await columnRepo.GetByIdAsync(workspaceId, columnId);
        if (column is null)
            throw new ColumnNotFoundException();

        var (changes, total) = await changeRepo.GetByColumnAsync(columnId, page.Limit, page.Offset);

        var items = changes.Select(c => new MetadataChangeDto(
            c.Id,
            c.Field,
            c.OldValue,
            c.NewValue,
            c.Participant.Email,
            c.EditedAt
        )).ToList();

        return new PagedResult<MetadataChangeDto>(items, total, page.Limit, page.Offset);
    }

    public async Task<PagedResult<string>> GetTableNamesAsync(Guid workspaceId, PageQuery page)
    {
        RequirePaging(page.Limit, page.Offset, MaxTableNameLimit);

        var (names, total) = await projectionRepo.GetDistinctTableNamesAsync(
            workspaceId, page.Limit, page.Offset);

        return new PagedResult<string>(names, total, page.Limit, page.Offset);
    }

    /// <summary>Copies the request onto the column, returning an audit record per changed field.</summary>
    private static List<MetadataChange> ApplyUpdate(
        Column column, ColumnUpdateRequest update, Guid participantId, DateTime now)
    {
        var changes = new List<MetadataChange>();

        void Track(string field, string? oldValue, string? newValue)
        {
            if (oldValue == newValue) return;

            changes.Add(new MetadataChange
            {
                Id = Guid.NewGuid(),
                EntityType = "Column",
                EntityId = column.Id,
                Field = field,
                OldValue = oldValue,
                NewValue = newValue,
                ParticipantId = participantId,
                EditedAt = now
            });
        }

        Track("Description", column.Description, update.Description);
        Track("ExampleValue", column.ExampleValue, update.ExampleValue);
        Track("Owner", column.Owner, update.Owner);

        column.Description = update.Description;
        column.ExampleValue = update.ExampleValue;
        column.Owner = update.Owner;
        column.Version++;
        column.UpdatedAt = now;

        return changes;
    }

    private static void ValidateUpdates(List<ColumnUpdateRequest> updates)
    {
        Require(updates.Count > 0, "No column updates were supplied.");
        Require(updates.Count <= MaxBulkUpdateRows,
            $"A single request may update at most {MaxBulkUpdateRows:N0} columns; received {updates.Count:N0}.");

        // Two edits to one column in one batch would bump Version twice and write an audit
        // trail that reads as though the first edit never happened.
        var duplicate = updates
            .GroupBy(u => u.ColumnId)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new ValidationException($"Column {duplicate.Key} appears more than once in the request.");

        foreach (var update in updates)
        {
            RequireMaxLength(update.Description, MaxDescriptionLength, "description", update.ColumnId);
            RequireMaxLength(update.ExampleValue, MaxExampleValueLength, "example value", update.ColumnId);
            RequireMaxLength(update.Owner, MaxOwnerLength, "owner", update.ColumnId);
        }
    }

    private static void RequireMaxLength(string? value, int maxLength, string field, Guid columnId)
    {
        Require(value is null || value.Length <= maxLength,
            $"The {field} for column {columnId} is longer than the {maxLength:N0} character limit.");
    }
}
