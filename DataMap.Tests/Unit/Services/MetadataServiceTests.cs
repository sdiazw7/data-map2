using DataMap.Api.Data;
using DataMap.Api.DTOs;
using DataMap.Api.Exceptions;
using DataMap.Api.Models;
using DataMap.Api.Repositories;
using DataMap.Api.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace DataMap.Tests.Unit.Services;

public class MetadataServiceTests
{
    private readonly Mock<IColumnRepository> _columnRepo = new();
    private readonly Mock<IProjectionRepository> _projectionRepo = new();
    private readonly Mock<IMetadataChangeRepository> _changeRepo = new();
    private readonly Mock<IProjectionService> _projectionService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<MetadataService>> _logger = new();

    public MetadataServiceTests()
    {
        // Run the transactional body inline. Whether the transaction actually commits is EF's
        // job; what these tests care about is which calls happen inside it.
        _unitOfWork.Setup(u => u.ExecuteAsync(It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(operation => operation());

        _columnRepo.Setup(r => r.UpdateRangeAsync(It.IsAny<IReadOnlyCollection<Column>>()))
            .ReturnsAsync(true);
    }

    private MetadataService CreateService() => new(
        _columnRepo.Object,
        _projectionRepo.Object,
        _changeRepo.Object,
        _projectionService.Object,
        _unitOfWork.Object,
        _logger.Object);

    private void SetupColumns(Guid workspaceId, params Column[] columns)
    {
        _columnRepo.Setup(r => r.GetByIdsAsync(workspaceId, It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(columns.ToList());
    }

    // ── GetColumnsAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetColumnsAsync_ReturnsRowsMappedToDtos()
    {
        var workspaceId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        var rows = new List<ColumnCatalogEditor>
        {
            new() { ColumnId = columnId, SchemaName = "sales", TableName = "orders", ColumnName = "id",
                    DataType = "uuid", Description = "PK", ExampleValue = "abc", Owner = "alice",
                    BusinessTerm = "Order ID", Version = 3 }
        };
        _projectionRepo.Setup(r => r.QueryAsync(workspaceId, 200, 0, null, false, null, "columnName", "asc"))
            .ReturnsAsync((rows, 1));

        var result = await CreateService().GetColumnsAsync(workspaceId, new MetadataColumnsQuery());

        Assert.Single(result.Items);
        Assert.Equal(columnId, result.Items[0].ColumnId);
        Assert.Equal("sales", result.Items[0].SchemaName);
        Assert.Equal("orders", result.Items[0].TableName);
        Assert.Equal("id", result.Items[0].ColumnName);
        Assert.Equal("uuid", result.Items[0].DataType);
        Assert.Equal("PK", result.Items[0].Description);
        Assert.Equal("abc", result.Items[0].ExampleValue);
        Assert.Equal("alice", result.Items[0].Owner);
        Assert.Equal("Order ID", result.Items[0].BusinessTerm);
        Assert.Equal(3, result.Items[0].Version);
    }

    [Fact]
    public async Task GetColumnsAsync_EmptyResult_ReturnsEmptyList()
    {
        var workspaceId = Guid.NewGuid();
        _projectionRepo.Setup(r => r.QueryAsync(workspaceId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((new List<ColumnCatalogEditor>(), 0));

        var result = await CreateService().GetColumnsAsync(workspaceId, new MetadataColumnsQuery());

        Assert.Empty(result.Items);
        Assert.Equal(0, result.Total);
    }

    [Fact]
    public async Task GetColumnsAsync_ReportsRepoTotalNotPageLength()
    {
        var workspaceId = Guid.NewGuid();
        var rows = new List<ColumnCatalogEditor>
        {
            new() { ColumnId = Guid.NewGuid(), SchemaName = "sales", TableName = "orders",
                    ColumnName = "id", DataType = "uuid", Version = 1 }
        };
        _projectionRepo.Setup(r => r.QueryAsync(workspaceId, 1, 0, null, false, null, "columnName", "asc"))
            .ReturnsAsync((rows, 104318));

        var result = await CreateService().GetColumnsAsync(workspaceId, new MetadataColumnsQuery(Limit: 1));

        Assert.Single(result.Items);
        Assert.Equal(104318, result.Total);
        Assert.Equal(1, result.Limit);
        Assert.Equal(0, result.Offset);
    }

    [Fact]
    public async Task GetColumnsAsync_PassesAllQueryParamsToRepo()
    {
        var workspaceId = Guid.NewGuid();
        var query = new MetadataColumnsQuery(Limit: 50, Offset: 100, Search: "customer", UndocumentedOnly: true);
        _projectionRepo.Setup(r => r.QueryAsync(workspaceId, 50, 100, "customer", true, null, "columnName", "asc")).ReturnsAsync((new List<ColumnCatalogEditor>(), 0));

        await CreateService().GetColumnsAsync(workspaceId, query);

        _projectionRepo.Verify(r => r.QueryAsync(workspaceId, 50, 100, "customer", true, null, "columnName", "asc"), Times.Once);
    }

    [Fact]
    public async Task GetColumnsAsync_SortByDescription_ThrowsValidationException()
    {
        var workspaceId = Guid.NewGuid();
        var query = new MetadataColumnsQuery(SortBy: "description");

        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().GetColumnsAsync(workspaceId, query));
    }

    [Fact]
    public async Task GetColumnsAsync_StorageStyleSortName_ThrowsValidationException()
    {
        // sortBy takes the response field names now; the old snake_case spelling is not a
        // second accepted vocabulary.
        var workspaceId = Guid.NewGuid();

        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().GetColumnsAsync(workspaceId, new MetadataColumnsQuery(SortBy: "column_name")));
    }

    [Theory]
    [InlineData("columnName")]
    [InlineData("tableName")]
    [InlineData("dataType")]
    [InlineData("owner")]
    public async Task GetColumnsAsync_SortableFields_AreAccepted(string sortBy)
    {
        var workspaceId = Guid.NewGuid();
        _projectionRepo.Setup(r => r.QueryAsync(workspaceId, 200, 0, null, false, null, sortBy, "asc"))
            .ReturnsAsync((new List<ColumnCatalogEditor>(), 0));

        await CreateService().GetColumnsAsync(workspaceId, new MetadataColumnsQuery(SortBy: sortBy));

        _projectionRepo.Verify(r => r.QueryAsync(workspaceId, 200, 0, null, false, null, sortBy, "asc"), Times.Once);
    }

    [Fact]
    public async Task GetColumnsAsync_InvalidSortDir_ThrowsValidationException()
    {
        var workspaceId = Guid.NewGuid();
        var query = new MetadataColumnsQuery(SortDir: "sideways");

        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().GetColumnsAsync(workspaceId, query));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    [InlineData(int.MaxValue)]
    public async Task GetColumnsAsync_LimitOutOfRange_ThrowsValidationException(int limit)
    {
        var workspaceId = Guid.NewGuid();

        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().GetColumnsAsync(workspaceId, new MetadataColumnsQuery(Limit: limit)));

        _projectionRepo.Verify(r => r.QueryAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetColumnsAsync_NegativeOffset_ThrowsValidationException()
    {
        var workspaceId = Guid.NewGuid();

        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().GetColumnsAsync(workspaceId, new MetadataColumnsQuery(Offset: -1)));
    }

    [Fact]
    public async Task GetColumnsAsync_MaxLimit_IsAccepted()
    {
        var workspaceId = Guid.NewGuid();
        _projectionRepo.Setup(r => r.QueryAsync(workspaceId, 1000, 0, null, false, null, "columnName", "asc")).ReturnsAsync((new List<ColumnCatalogEditor>(), 0));

        await CreateService().GetColumnsAsync(workspaceId, new MetadataColumnsQuery(Limit: 1000));

        _projectionRepo.Verify(r => r.QueryAsync(workspaceId, 1000, 0, null, false, null, "columnName", "asc"), Times.Once);
    }

    // ── BulkUpdateAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task BulkUpdateAsync_VersionConflict_ThrowsVersionConflictException()
    {
        var workspaceId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        SetupColumns(workspaceId, new Column { Id = columnId, Version = 2 });

        await Assert.ThrowsAsync<VersionConflictException>(() =>
            CreateService().BulkUpdateAsync(workspaceId, Guid.NewGuid(),
                [new ColumnUpdateRequest(columnId, "desc", null, null, 1)])); // version 1 != 2
    }

    [Fact]
    public async Task BulkUpdateAsync_StaleColumnLaterInBatch_WritesNothingAtAll()
    {
        // The regression this guards: writes used to commit per column as the loop ran, so a
        // conflict on a later row left the earlier rows saved with no audit trail and a stale
        // projection — which then rejected every subsequent edit to those cells.
        var workspaceId = Guid.NewGuid();
        var freshId = Guid.NewGuid();
        var staleId = Guid.NewGuid();
        SetupColumns(workspaceId,
            new Column { Id = freshId, Version = 1, Description = "old" },
            new Column { Id = staleId, Version = 7, Description = "old" });

        await Assert.ThrowsAsync<VersionConflictException>(() =>
            CreateService().BulkUpdateAsync(workspaceId, Guid.NewGuid(),
            [
                new ColumnUpdateRequest(freshId, "new", null, null, 1),
                new ColumnUpdateRequest(staleId, "new", null, null, 3) // stale
            ]));

        _columnRepo.Verify(r => r.UpdateRangeAsync(It.IsAny<IReadOnlyCollection<Column>>()), Times.Never);
        _changeRepo.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<MetadataChange>>()), Times.Never);
        _projectionService.Verify(p => p.SyncColumnsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Column>>()), Times.Never);
    }

    [Fact]
    public async Task BulkUpdateAsync_StaleColumnLaterInBatch_LeavesEarlierColumnUntouched()
    {
        var workspaceId = Guid.NewGuid();
        var freshId = Guid.NewGuid();
        var staleId = Guid.NewGuid();
        var fresh = new Column { Id = freshId, Version = 1, Description = "old" };
        SetupColumns(workspaceId, fresh, new Column { Id = staleId, Version = 7 });

        await Assert.ThrowsAsync<VersionConflictException>(() =>
            CreateService().BulkUpdateAsync(workspaceId, Guid.NewGuid(),
            [
                new ColumnUpdateRequest(freshId, "new", null, null, 1),
                new ColumnUpdateRequest(staleId, "new", null, null, 3)
            ]));

        Assert.Equal(1, fresh.Version);
        Assert.Equal("old", fresh.Description);
    }

    [Fact]
    public async Task BulkUpdateAsync_ConcurrencyTokenRejectsWrite_ThrowsVersionConflictException()
    {
        var workspaceId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        SetupColumns(workspaceId, new Column { Id = columnId, Version = 1, Description = "old" });
        _columnRepo.Setup(r => r.UpdateRangeAsync(It.IsAny<IReadOnlyCollection<Column>>()))
            .ReturnsAsync(false); // database rejected the write: someone else got there first

        await Assert.ThrowsAsync<VersionConflictException>(() =>
            CreateService().BulkUpdateAsync(workspaceId, Guid.NewGuid(),
                [new ColumnUpdateRequest(columnId, "new", null, null, 1)]));

        _changeRepo.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<MetadataChange>>()), Times.Never);
    }

    [Fact]
    public async Task BulkUpdateAsync_ColumnNotFound_SkipsAndDoesNotThrow()
    {
        var workspaceId = Guid.NewGuid();
        var missingId = Guid.NewGuid();
        SetupColumns(workspaceId);

        await CreateService().BulkUpdateAsync(workspaceId, Guid.NewGuid(),
            [new ColumnUpdateRequest(missingId, null, null, null, 0)]);

        _columnRepo.Verify(r => r.UpdateRangeAsync(
            It.Is<IReadOnlyCollection<Column>>(c => c.Count == 0)), Times.Once);
    }

    [Fact]
    public async Task BulkUpdateAsync_LoadsAllColumnsInOneQuery()
    {
        var workspaceId = Guid.NewGuid();
        var col1Id = Guid.NewGuid();
        var col2Id = Guid.NewGuid();
        SetupColumns(workspaceId,
            new Column { Id = col1Id, Version = 1 },
            new Column { Id = col2Id, Version = 1 });

        await CreateService().BulkUpdateAsync(workspaceId, Guid.NewGuid(),
        [
            new ColumnUpdateRequest(col1Id, "a", null, null, 1),
            new ColumnUpdateRequest(col2Id, "b", null, null, 1)
        ]);

        _columnRepo.Verify(r => r.GetByIdsAsync(workspaceId, It.Is<IReadOnlyCollection<Guid>>(
            ids => ids.Count == 2)), Times.Once);
        _columnRepo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task BulkUpdateAsync_DescriptionChanged_CreatesAuditRecord()
    {
        var workspaceId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        SetupColumns(workspaceId, new Column { Id = columnId, Version = 1, Description = "old" });

        await CreateService().BulkUpdateAsync(workspaceId, participantId,
            [new ColumnUpdateRequest(columnId, "new", null, null, 1)]);

        _changeRepo.Verify(r => r.AddRangeAsync(It.Is<IEnumerable<MetadataChange>>(
            changes => changes.Any(c => c.Field == "Description" && c.OldValue == "old" && c.NewValue == "new")
        )), Times.Once);
    }

    [Fact]
    public async Task BulkUpdateAsync_ExampleValueChanged_CreatesAuditRecord()
    {
        var workspaceId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        SetupColumns(workspaceId, new Column { Id = columnId, Version = 1, ExampleValue = "old_val" });

        await CreateService().BulkUpdateAsync(workspaceId, participantId,
            [new ColumnUpdateRequest(columnId, null, "new_val", null, 1)]);

        _changeRepo.Verify(r => r.AddRangeAsync(It.Is<IEnumerable<MetadataChange>>(
            changes => changes.Any(c => c.Field == "ExampleValue" && c.OldValue == "old_val" && c.NewValue == "new_val")
        )), Times.Once);
    }

    [Fact]
    public async Task BulkUpdateAsync_OwnerChanged_CreatesAuditRecord()
    {
        var workspaceId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        SetupColumns(workspaceId, new Column { Id = columnId, Version = 1, Owner = "alice" });

        await CreateService().BulkUpdateAsync(workspaceId, participantId,
            [new ColumnUpdateRequest(columnId, null, null, "bob", 1)]);

        _changeRepo.Verify(r => r.AddRangeAsync(It.Is<IEnumerable<MetadataChange>>(
            changes => changes.Any(c => c.Field == "Owner" && c.OldValue == "alice" && c.NewValue == "bob")
        )), Times.Once);
    }

    [Fact]
    public async Task BulkUpdateAsync_NoFieldsChanged_DoesNotCreateAuditRecords()
    {
        var workspaceId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        SetupColumns(workspaceId, new Column { Id = columnId, Version = 1, Description = "same", ExampleValue = "same", Owner = "same" });

        await CreateService().BulkUpdateAsync(workspaceId, Guid.NewGuid(),
            [new ColumnUpdateRequest(columnId, "same", "same", "same", 1)]);

        _changeRepo.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<MetadataChange>>()), Times.Never);
    }

    [Fact]
    public async Task BulkUpdateAsync_IncrementsVersionOnUpdate()
    {
        var workspaceId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        var column = new Column { Id = columnId, Version = 3, Description = "old" };
        SetupColumns(workspaceId, column);

        await CreateService().BulkUpdateAsync(workspaceId, Guid.NewGuid(),
            [new ColumnUpdateRequest(columnId, "new", null, null, 3)]);

        Assert.Equal(4, column.Version);
    }

    [Fact]
    public async Task BulkUpdateAsync_MultipleColumns_AuditRecordsForEachChangedField()
    {
        var workspaceId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var col1Id = Guid.NewGuid();
        var col2Id = Guid.NewGuid();
        SetupColumns(workspaceId,
            new Column { Id = col1Id, Version = 1, Description = "a" },
            new Column { Id = col2Id, Version = 1, Description = "b" });

        await CreateService().BulkUpdateAsync(workspaceId, participantId,
        [
            new ColumnUpdateRequest(col1Id, "a_new", null, null, 1),
            new ColumnUpdateRequest(col2Id, "b_new", null, null, 1)
        ]);

        _changeRepo.Verify(r => r.AddRangeAsync(It.Is<IEnumerable<MetadataChange>>(
            changes => changes.Count() == 2
        )), Times.Once);
    }

    [Fact]
    public async Task BulkUpdateAsync_SyncsOnlyTheUpdatedColumns()
    {
        var workspaceId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        SetupColumns(workspaceId, new Column { Id = columnId, Version = 1, Description = "old" });

        await CreateService().BulkUpdateAsync(workspaceId, Guid.NewGuid(),
            [new ColumnUpdateRequest(columnId, "new", null, null, 1)]);

        _projectionService.Verify(p => p.SyncColumnsAsync(workspaceId,
            It.Is<IReadOnlyCollection<Column>>(c => c.Count == 1 && c.Single().Id == columnId)), Times.Once);
    }

    [Fact]
    public async Task BulkUpdateAsync_NeverRebuildsWholeProjection()
    {
        var workspaceId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        SetupColumns(workspaceId, new Column { Id = columnId, Version = 1, Description = "old" });

        await CreateService().BulkUpdateAsync(workspaceId, Guid.NewGuid(),
            [new ColumnUpdateRequest(columnId, "new", null, null, 1)]);

        _projectionService.Verify(p => p.RefreshAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task BulkUpdateAsync_ColumnNotFound_ExcludedFromProjectionSync()
    {
        var workspaceId = Guid.NewGuid();
        var missingId = Guid.NewGuid();
        SetupColumns(workspaceId);

        await CreateService().BulkUpdateAsync(workspaceId, Guid.NewGuid(),
            [new ColumnUpdateRequest(missingId, null, null, null, 0)]);

        _projectionService.Verify(p => p.SyncColumnsAsync(workspaceId,
            It.Is<IReadOnlyCollection<Column>>(c => c.Count == 0)), Times.Once);
    }

    [Fact]
    public async Task BulkUpdateAsync_PersistsAuditAndProjectionInsideOneTransaction()
    {
        var workspaceId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        SetupColumns(workspaceId, new Column { Id = columnId, Version = 1, Description = "old" });

        await CreateService().BulkUpdateAsync(workspaceId, Guid.NewGuid(),
            [new ColumnUpdateRequest(columnId, "new", null, null, 1)]);

        _unitOfWork.Verify(u => u.ExecuteAsync(It.IsAny<Func<Task>>()), Times.Once);
    }

    [Fact]
    public async Task BulkUpdateAsync_EmptyRequest_ThrowsValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().BulkUpdateAsync(Guid.NewGuid(), Guid.NewGuid(), []));
    }

    [Fact]
    public async Task BulkUpdateAsync_TooManyRows_ThrowsValidationException()
    {
        var updates = Enumerable.Range(0, 5_001)
            .Select(_ => new ColumnUpdateRequest(Guid.NewGuid(), "d", null, null, 1))
            .ToList();

        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().BulkUpdateAsync(Guid.NewGuid(), Guid.NewGuid(), updates));
    }

    [Fact]
    public async Task BulkUpdateAsync_DuplicateColumnInBatch_ThrowsValidationException()
    {
        var columnId = Guid.NewGuid();

        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().BulkUpdateAsync(Guid.NewGuid(), Guid.NewGuid(),
            [
                new ColumnUpdateRequest(columnId, "first", null, null, 1),
                new ColumnUpdateRequest(columnId, "second", null, null, 1)
            ]));
    }

    [Fact]
    public async Task BulkUpdateAsync_OverlongDescription_ThrowsValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().BulkUpdateAsync(Guid.NewGuid(), Guid.NewGuid(),
                [new ColumnUpdateRequest(Guid.NewGuid(), new string('x', 4_001), null, null, 1)]));
    }

    [Fact]
    public async Task BulkUpdateAsync_OverlongOwner_ThrowsValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().BulkUpdateAsync(Guid.NewGuid(), Guid.NewGuid(),
                [new ColumnUpdateRequest(Guid.NewGuid(), null, null, new string('x', 201), 1)]));
    }

    [Fact]
    public async Task BulkUpdateAsync_InvalidRequest_ReadsNothing()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().BulkUpdateAsync(Guid.NewGuid(), Guid.NewGuid(), []));

        _columnRepo.Verify(r => r.GetByIdsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>()), Times.Never);
    }

    // ── GetCoverageAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetCoverageAsync_CalculatesPercentageCorrectly()
    {
        var workspaceId = Guid.NewGuid();
        _projectionRepo.Setup(r => r.GetCoverageCountsAsync(workspaceId)).ReturnsAsync((100, 75));

        var result = await CreateService().GetCoverageAsync(workspaceId);

        Assert.Equal(100, result.TotalColumns);
        Assert.Equal(75, result.DocumentedColumns);
        Assert.Equal(75.0, result.CoveragePercent);
    }

    [Fact]
    public async Task GetCoverageAsync_ZeroTotal_ReturnsZeroPercent()
    {
        var workspaceId = Guid.NewGuid();
        _projectionRepo.Setup(r => r.GetCoverageCountsAsync(workspaceId)).ReturnsAsync((0, 0));

        var result = await CreateService().GetCoverageAsync(workspaceId);

        Assert.Equal(0, result.TotalColumns);
        Assert.Equal(0.0, result.CoveragePercent);
    }

    [Fact]
    public async Task GetCoverageAsync_AllDocumented_Returns100Percent()
    {
        var workspaceId = Guid.NewGuid();
        _projectionRepo.Setup(r => r.GetCoverageCountsAsync(workspaceId)).ReturnsAsync((50, 50));

        var result = await CreateService().GetCoverageAsync(workspaceId);

        Assert.Equal(100.0, result.CoveragePercent);
    }

    [Fact]
    public async Task GetCoverageAsync_RoundsToOneDecimalPlace()
    {
        var workspaceId = Guid.NewGuid();
        _projectionRepo.Setup(r => r.GetCoverageCountsAsync(workspaceId)).ReturnsAsync((3, 1));

        var result = await CreateService().GetCoverageAsync(workspaceId);

        // 1/3 = 33.333... → rounds to 33.3
        Assert.Equal(33.3, result.CoveragePercent);
    }
}
