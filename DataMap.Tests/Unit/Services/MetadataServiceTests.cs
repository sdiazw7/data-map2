using DataMap.Api.DTOs;
using DataMap.Api.Exceptions;
using DataMap.Api.Models;
using DataMap.Api.Repositories;
using DataMap.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;

namespace DataMap.Tests.Unit.Services;

public class MetadataServiceTests
{
    private readonly Mock<IColumnRepository> _columnRepo = new();
    private readonly Mock<ISchemaRepository> _schemaRepo = new();
    private readonly Mock<ITableRepository> _tableRepo = new();
    private readonly Mock<IProjectionRepository> _projectionRepo = new();
    private readonly Mock<IMetadataChangeRepository> _changeRepo = new();
    private readonly Mock<IProjectionService> _projectionService = new();
    private readonly Mock<ILogger<MetadataService>> _logger = new();

    private MetadataService CreateService() => new(
        _columnRepo.Object,
        _schemaRepo.Object,
        _tableRepo.Object,
        _projectionRepo.Object,
        _changeRepo.Object,
        _projectionService.Object,
        _logger.Object);

    private static IFormFile MakeCsvFile(string csvContent)
    {
        var bytes = Encoding.UTF8.GetBytes(csvContent);
        var mock = new Mock<IFormFile>();
        mock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(bytes));
        return mock.Object;
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
        _projectionRepo.Setup(r => r.QueryAsync(workspaceId, 200, 0, null, false, null, "column_name", "asc")).ReturnsAsync(rows);

        var result = await CreateService().GetColumnsAsync(workspaceId, new MetadataColumnsQuery());

        Assert.Single(result);
        Assert.Equal(columnId, result[0].ColumnId);
        Assert.Equal("sales", result[0].SchemaName);
        Assert.Equal("orders", result[0].TableName);
        Assert.Equal("id", result[0].ColumnName);
        Assert.Equal("uuid", result[0].DataType);
        Assert.Equal("PK", result[0].Description);
        Assert.Equal("abc", result[0].ExampleValue);
        Assert.Equal("alice", result[0].Owner);
        Assert.Equal("Order ID", result[0].BusinessTerm);
        Assert.Equal(3, result[0].Version);
    }

    [Fact]
    public async Task GetColumnsAsync_EmptyResult_ReturnsEmptyList()
    {
        var workspaceId = Guid.NewGuid();
        _projectionRepo.Setup(r => r.QueryAsync(workspaceId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync([]);

        var result = await CreateService().GetColumnsAsync(workspaceId, new MetadataColumnsQuery());

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetColumnsAsync_PassesAllQueryParamsToRepo()
    {
        var workspaceId = Guid.NewGuid();
        var query = new MetadataColumnsQuery(Limit: 50, Offset: 100, Search: "customer", UndocumentedOnly: true);
        _projectionRepo.Setup(r => r.QueryAsync(workspaceId, 50, 100, "customer", true, null, "column_name", "asc")).ReturnsAsync([]);

        await CreateService().GetColumnsAsync(workspaceId, query);

        _projectionRepo.Verify(r => r.QueryAsync(workspaceId, 50, 100, "customer", true, null, "column_name", "asc"), Times.Once);
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
    public async Task GetColumnsAsync_InvalidSortDir_ThrowsValidationException()
    {
        var workspaceId = Guid.NewGuid();
        var query = new MetadataColumnsQuery(SortDir: "sideways");

        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().GetColumnsAsync(workspaceId, query));
    }

    // ── UploadCsvAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task UploadCsvAsync_ValidCsv_UpsertsEachColumn()
    {
        var workspaceId = Guid.NewGuid();
        var schemaId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var csv = "schema_name,table_name,column_name,data_type\nsales,orders,id,uuid\nsales,orders,total,numeric";

        _schemaRepo.Setup(r => r.UpsertAsync(workspaceId, "sales")).ReturnsAsync(new DataMap.Api.Models.Schema { Id = schemaId });
        _tableRepo.Setup(r => r.UpsertAsync(workspaceId, schemaId, "orders")).ReturnsAsync(new Table { Id = tableId });
        _columnRepo.Setup(r => r.UpsertAsync(workspaceId, tableId, It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(new Column { Id = Guid.NewGuid() });

        await CreateService().UploadCsvAsync(workspaceId, Guid.NewGuid(), MakeCsvFile(csv));

        _columnRepo.Verify(r => r.UpsertAsync(workspaceId, tableId, "id", "uuid"), Times.Once);
        _columnRepo.Verify(r => r.UpsertAsync(workspaceId, tableId, "total", "numeric"), Times.Once);
    }

    [Fact]
    public async Task UploadCsvAsync_SameSchemaOnMultipleRows_UpsertsSchemaOnlyOnce()
    {
        var workspaceId = Guid.NewGuid();
        var schemaId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var csv = "schema_name,table_name,column_name,data_type\nsales,orders,id,uuid\nsales,orders,total,numeric";

        _schemaRepo.Setup(r => r.UpsertAsync(workspaceId, "sales")).ReturnsAsync(new DataMap.Api.Models.Schema { Id = schemaId });
        _tableRepo.Setup(r => r.UpsertAsync(workspaceId, schemaId, "orders")).ReturnsAsync(new Table { Id = tableId });
        _columnRepo.Setup(r => r.UpsertAsync(workspaceId, tableId, It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(new Column { Id = Guid.NewGuid() });

        await CreateService().UploadCsvAsync(workspaceId, Guid.NewGuid(), MakeCsvFile(csv));

        _schemaRepo.Verify(r => r.UpsertAsync(workspaceId, "sales"), Times.Once);
    }

    [Fact]
    public async Task UploadCsvAsync_SameTableOnMultipleRows_UpsertsTableOnlyOnce()
    {
        var workspaceId = Guid.NewGuid();
        var schemaId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var csv = "schema_name,table_name,column_name,data_type\nsales,orders,id,uuid\nsales,orders,total,numeric";

        _schemaRepo.Setup(r => r.UpsertAsync(workspaceId, "sales")).ReturnsAsync(new DataMap.Api.Models.Schema { Id = schemaId });
        _tableRepo.Setup(r => r.UpsertAsync(workspaceId, schemaId, "orders")).ReturnsAsync(new Table { Id = tableId });
        _columnRepo.Setup(r => r.UpsertAsync(workspaceId, tableId, It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(new Column { Id = Guid.NewGuid() });

        await CreateService().UploadCsvAsync(workspaceId, Guid.NewGuid(), MakeCsvFile(csv));

        _tableRepo.Verify(r => r.UpsertAsync(workspaceId, schemaId, "orders"), Times.Once);
    }

    [Fact]
    public async Task UploadCsvAsync_MultipleSchemas_UpsertsEachSchemaOnce()
    {
        var workspaceId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var mktId = Guid.NewGuid();
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        var csv = "schema_name,table_name,column_name,data_type\nsales,orders,id,uuid\nmarketing,campaigns,name,varchar";

        _schemaRepo.Setup(r => r.UpsertAsync(workspaceId, "sales")).ReturnsAsync(new DataMap.Api.Models.Schema { Id = salesId });
        _schemaRepo.Setup(r => r.UpsertAsync(workspaceId, "marketing")).ReturnsAsync(new DataMap.Api.Models.Schema { Id = mktId });
        _tableRepo.Setup(r => r.UpsertAsync(workspaceId, salesId, "orders")).ReturnsAsync(new Table { Id = t1 });
        _tableRepo.Setup(r => r.UpsertAsync(workspaceId, mktId, "campaigns")).ReturnsAsync(new Table { Id = t2 });
        _columnRepo.Setup(r => r.UpsertAsync(workspaceId, It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(new Column { Id = Guid.NewGuid() });

        await CreateService().UploadCsvAsync(workspaceId, Guid.NewGuid(), MakeCsvFile(csv));

        _schemaRepo.Verify(r => r.UpsertAsync(workspaceId, "sales"), Times.Once);
        _schemaRepo.Verify(r => r.UpsertAsync(workspaceId, "marketing"), Times.Once);
    }

    [Fact]
    public async Task UploadCsvAsync_RefreshesProjection()
    {
        var workspaceId = Guid.NewGuid();
        var schemaId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var csv = "schema_name,table_name,column_name,data_type\nsales,orders,id,uuid";

        _schemaRepo.Setup(r => r.UpsertAsync(workspaceId, "sales")).ReturnsAsync(new DataMap.Api.Models.Schema { Id = schemaId });
        _tableRepo.Setup(r => r.UpsertAsync(workspaceId, schemaId, "orders")).ReturnsAsync(new Table { Id = tableId });
        _columnRepo.Setup(r => r.UpsertAsync(workspaceId, tableId, It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(new Column { Id = Guid.NewGuid() });

        await CreateService().UploadCsvAsync(workspaceId, Guid.NewGuid(), MakeCsvFile(csv));

        _projectionService.Verify(p => p.RefreshAsync(workspaceId), Times.Once);
    }

    // ── BulkUpdateAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task BulkUpdateAsync_VersionConflict_ThrowsVersionConflictException()
    {
        var workspaceId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        var column = new Column { Id = columnId, Version = 2 };
        _columnRepo.Setup(r => r.GetByIdAsync(workspaceId, columnId)).ReturnsAsync(column);

        await Assert.ThrowsAsync<VersionConflictException>(() =>
            CreateService().BulkUpdateAsync(workspaceId, Guid.NewGuid(),
                [new ColumnUpdateRequest(columnId, "desc", null, null, 1)])); // version 1 != 2
    }

    [Fact]
    public async Task BulkUpdateAsync_ColumnNotFound_SkipsAndDoesNotThrow()
    {
        var workspaceId = Guid.NewGuid();
        var missingId = Guid.NewGuid();
        _columnRepo.Setup(r => r.GetByIdAsync(workspaceId, missingId)).ReturnsAsync((Column?)null);

        await CreateService().BulkUpdateAsync(workspaceId, Guid.NewGuid(),
            [new ColumnUpdateRequest(missingId, null, null, null, 0)]);

        _columnRepo.Verify(r => r.UpdateAsync(It.IsAny<Column>()), Times.Never);
    }

    [Fact]
    public async Task BulkUpdateAsync_DescriptionChanged_CreatesAuditRecord()
    {
        var workspaceId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        var column = new Column { Id = columnId, Version = 1, Description = "old" };
        _columnRepo.Setup(r => r.GetByIdAsync(workspaceId, columnId)).ReturnsAsync(column);
        _columnRepo.Setup(r => r.UpdateAsync(It.IsAny<Column>())).ReturnsAsync(true);

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
        var column = new Column { Id = columnId, Version = 1, ExampleValue = "old_val" };
        _columnRepo.Setup(r => r.GetByIdAsync(workspaceId, columnId)).ReturnsAsync(column);
        _columnRepo.Setup(r => r.UpdateAsync(It.IsAny<Column>())).ReturnsAsync(true);

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
        var column = new Column { Id = columnId, Version = 1, Owner = "alice" };
        _columnRepo.Setup(r => r.GetByIdAsync(workspaceId, columnId)).ReturnsAsync(column);
        _columnRepo.Setup(r => r.UpdateAsync(It.IsAny<Column>())).ReturnsAsync(true);

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
        var column = new Column { Id = columnId, Version = 1, Description = "same", ExampleValue = "same", Owner = "same" };
        _columnRepo.Setup(r => r.GetByIdAsync(workspaceId, columnId)).ReturnsAsync(column);
        _columnRepo.Setup(r => r.UpdateAsync(It.IsAny<Column>())).ReturnsAsync(true);

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
        Column? saved = null;

        _columnRepo.Setup(r => r.GetByIdAsync(workspaceId, columnId)).ReturnsAsync(column);
        _columnRepo.Setup(r => r.UpdateAsync(It.IsAny<Column>()))
            .Callback<Column>(c => saved = c)
            .ReturnsAsync(true);

        await CreateService().BulkUpdateAsync(workspaceId, Guid.NewGuid(),
            [new ColumnUpdateRequest(columnId, "new", null, null, 3)]);

        Assert.Equal(4, saved!.Version);
    }

    [Fact]
    public async Task BulkUpdateAsync_MultipleColumns_AuditRecordsForEachChangedField()
    {
        var workspaceId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var col1Id = Guid.NewGuid();
        var col2Id = Guid.NewGuid();
        var col1 = new Column { Id = col1Id, Version = 1, Description = "a" };
        var col2 = new Column { Id = col2Id, Version = 1, Description = "b" };

        _columnRepo.Setup(r => r.GetByIdAsync(workspaceId, col1Id)).ReturnsAsync(col1);
        _columnRepo.Setup(r => r.GetByIdAsync(workspaceId, col2Id)).ReturnsAsync(col2);
        _columnRepo.Setup(r => r.UpdateAsync(It.IsAny<Column>())).ReturnsAsync(true);

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
        var column = new Column { Id = columnId, Version = 1, Description = "old" };
        _columnRepo.Setup(r => r.GetByIdAsync(workspaceId, columnId)).ReturnsAsync(column);
        _columnRepo.Setup(r => r.UpdateAsync(It.IsAny<Column>())).ReturnsAsync(true);

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
        var column = new Column { Id = columnId, Version = 1, Description = "old" };
        _columnRepo.Setup(r => r.GetByIdAsync(workspaceId, columnId)).ReturnsAsync(column);
        _columnRepo.Setup(r => r.UpdateAsync(It.IsAny<Column>())).ReturnsAsync(true);

        await CreateService().BulkUpdateAsync(workspaceId, Guid.NewGuid(),
            [new ColumnUpdateRequest(columnId, "new", null, null, 1)]);

        _projectionService.Verify(p => p.RefreshAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task BulkUpdateAsync_ColumnNotFound_ExcludedFromProjectionSync()
    {
        var workspaceId = Guid.NewGuid();
        var missingId = Guid.NewGuid();
        _columnRepo.Setup(r => r.GetByIdAsync(workspaceId, missingId)).ReturnsAsync((Column?)null);

        await CreateService().BulkUpdateAsync(workspaceId, Guid.NewGuid(),
            [new ColumnUpdateRequest(missingId, null, null, null, 0)]);

        _projectionService.Verify(p => p.SyncColumnsAsync(workspaceId,
            It.Is<IReadOnlyCollection<Column>>(c => c.Count == 0)), Times.Once);
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
