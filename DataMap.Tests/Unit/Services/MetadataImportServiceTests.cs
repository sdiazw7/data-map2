using DataMap.Api.Data;
using DataMap.Api.DTOs;
using DataMap.Api.Exceptions;
using DataMap.Api.Repositories;
using DataMap.Api.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;

namespace DataMap.Tests.Unit.Services;

public class MetadataImportServiceTests
{
    private readonly Mock<IColumnRepository> _columnRepo = new();
    private readonly Mock<ISchemaRepository> _schemaRepo = new();
    private readonly Mock<ITableRepository> _tableRepo = new();
    private readonly Mock<IProjectionService> _projectionService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<MetadataImportService>> _logger = new();

    public MetadataImportServiceTests()
    {
        // Run the transactional body inline; what matters here is which calls happen inside it.
        _unitOfWork.Setup(u => u.ExecuteAsync(It.IsAny<Func<Task<ImportSummary>>>()))
            .Returns<Func<Task<ImportSummary>>>(operation => operation());
    }

    private MetadataImportService CreateService() => new(
        _columnRepo.Object,
        _schemaRepo.Object,
        _tableRepo.Object,
        _projectionService.Object,
        _unitOfWork.Object,
        _logger.Object);

    private static CsvUpload MakeUpload(string csvContent, string fileName = "columns.csv")
    {
        var bytes = Encoding.UTF8.GetBytes(csvContent);
        return new CsvUpload(new MemoryStream(bytes), fileName, bytes.Length);
    }

    /// <summary>Wires the three batch upserts so an import reaches the projection refresh.</summary>
    private void SetupUpsertChain(Guid workspaceId, Dictionary<string, Guid> schemas,
        Dictionary<(Guid SchemaId, string Name), Guid> tables)
    {
        _schemaRepo.Setup(r => r.UpsertManyAsync(workspaceId, It.IsAny<IReadOnlyCollection<string>>()))
            .ReturnsAsync(schemas);
        _tableRepo.Setup(r => r.UpsertManyAsync(workspaceId, It.IsAny<IReadOnlyCollection<(Guid SchemaId, string Name)>>()))
            .ReturnsAsync(tables);
        _columnRepo.Setup(r => r.UpsertManyAsync(workspaceId, It.IsAny<IReadOnlyCollection<ColumnImport>>()))
            .ReturnsAsync(new ColumnUpsertResult(1, 0, false));
    }

    [Fact]
    public async Task ImportCsvAsync_ValidCsv_UpsertsEveryColumnInOneBatch()
    {
        var workspaceId = Guid.NewGuid();
        var schemaId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var csv = "schema_name,table_name,column_name,data_type\nsales,orders,id,uuid\nsales,orders,total,numeric";

        SetupUpsertChain(workspaceId,
            new Dictionary<string, Guid> { ["sales"] = schemaId },
            new Dictionary<(Guid SchemaId, string Name), Guid> { [(schemaId, "orders")] = tableId });

        IReadOnlyCollection<ColumnImport>? imported = null;
        _columnRepo.Setup(r => r.UpsertManyAsync(workspaceId, It.IsAny<IReadOnlyCollection<ColumnImport>>()))
            .Callback<Guid, IReadOnlyCollection<ColumnImport>>((_, c) => imported = c)
            .ReturnsAsync(new ColumnUpsertResult(2, 0, false));

        await CreateService().ImportCsvAsync(workspaceId, Guid.NewGuid(), MakeUpload(csv));

        _columnRepo.Verify(r => r.UpsertManyAsync(workspaceId, It.IsAny<IReadOnlyCollection<ColumnImport>>()), Times.Once);
        Assert.Equal(2, imported!.Count);
        Assert.Contains(imported, c => c.TableId == tableId && c.Name == "id" && c.DataType == "uuid");
        Assert.Contains(imported, c => c.TableId == tableId && c.Name == "total" && c.DataType == "numeric");
    }

    [Fact]
    public async Task ImportCsvAsync_DeduplicatesSchemasAndTables()
    {
        var workspaceId = Guid.NewGuid();
        var schemaId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var csv = "schema_name,table_name,column_name,data_type\nsales,orders,id,uuid\nsales,orders,total,numeric";

        SetupUpsertChain(workspaceId,
            new Dictionary<string, Guid> { ["sales"] = schemaId },
            new Dictionary<(Guid SchemaId, string Name), Guid> { [(schemaId, "orders")] = tableId });

        IReadOnlyCollection<(Guid SchemaId, string Name)>? tableKeys = null;
        _tableRepo.Setup(r => r.UpsertManyAsync(workspaceId, It.IsAny<IReadOnlyCollection<(Guid SchemaId, string Name)>>()))
            .Callback<Guid, IReadOnlyCollection<(Guid SchemaId, string Name)>>((_, t) => tableKeys = t)
            .ReturnsAsync(new Dictionary<(Guid SchemaId, string Name), Guid> { [(schemaId, "orders")] = tableId });

        await CreateService().ImportCsvAsync(workspaceId, Guid.NewGuid(), MakeUpload(csv));

        _schemaRepo.Verify(r => r.UpsertManyAsync(workspaceId, It.IsAny<IReadOnlyCollection<string>>()), Times.Once);
        Assert.Single(tableKeys!);
    }

    [Fact]
    public async Task ImportCsvAsync_MultipleSchemas_PassesEachToTheBatch()
    {
        var workspaceId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var mktId = Guid.NewGuid();
        var csv = "schema_name,table_name,column_name,data_type\nsales,orders,id,uuid\nmarketing,campaigns,name,varchar";

        SetupUpsertChain(workspaceId,
            new Dictionary<string, Guid> { ["sales"] = salesId, ["marketing"] = mktId },
            new Dictionary<(Guid SchemaId, string Name), Guid>
            {
                [(salesId, "orders")] = Guid.NewGuid(),
                [(mktId, "campaigns")] = Guid.NewGuid()
            });

        IReadOnlyCollection<string>? names = null;
        _schemaRepo.Setup(r => r.UpsertManyAsync(workspaceId, It.IsAny<IReadOnlyCollection<string>>()))
            .Callback<Guid, IReadOnlyCollection<string>>((_, n) => names = n)
            .ReturnsAsync(new Dictionary<string, Guid> { ["sales"] = salesId, ["marketing"] = mktId });

        await CreateService().ImportCsvAsync(workspaceId, Guid.NewGuid(), MakeUpload(csv));

        Assert.Contains("sales", names!);
        Assert.Contains("marketing", names!);
    }

    [Fact]
    public async Task ImportCsvAsync_RefreshesProjection()
    {
        var workspaceId = Guid.NewGuid();
        var schemaId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var csv = "schema_name,table_name,column_name,data_type\nsales,orders,id,uuid";

        SetupUpsertChain(workspaceId,
            new Dictionary<string, Guid> { ["sales"] = schemaId },
            new Dictionary<(Guid SchemaId, string Name), Guid> { [(schemaId, "orders")] = tableId });

        await CreateService().ImportCsvAsync(workspaceId, Guid.NewGuid(), MakeUpload(csv));

        _projectionService.Verify(p => p.RefreshAsync(workspaceId), Times.Once);
    }

    [Fact]
    public async Task ImportCsvAsync_RunsEntirelyInsideOneTransaction()
    {
        var workspaceId = Guid.NewGuid();
        var schemaId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var csv = "schema_name,table_name,column_name,data_type\nsales,orders,id,uuid";

        SetupUpsertChain(workspaceId,
            new Dictionary<string, Guid> { ["sales"] = schemaId },
            new Dictionary<(Guid SchemaId, string Name), Guid> { [(schemaId, "orders")] = tableId });

        await CreateService().ImportCsvAsync(workspaceId, Guid.NewGuid(), MakeUpload(csv));

        _unitOfWork.Verify(u => u.ExecuteAsync(It.IsAny<Func<Task<ImportSummary>>>()), Times.Once);
    }

    [Fact]
    public async Task ImportCsvAsync_ReturnsSummaryOfWhatWasImported()
    {
        var workspaceId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        var csv = "schema_name,table_name,column_name,data_type\n"
                + "sales,orders,id,uuid\nsales,customers,id,uuid";

        SetupUpsertChain(workspaceId,
            new Dictionary<string, Guid> { ["sales"] = salesId },
            new Dictionary<(Guid SchemaId, string Name), Guid>
            {
                [(salesId, "orders")] = t1,
                [(salesId, "customers")] = t2
            });
        _columnRepo.Setup(r => r.UpsertManyAsync(workspaceId, It.IsAny<IReadOnlyCollection<ColumnImport>>()))
            .ReturnsAsync(new ColumnUpsertResult(2, 1, false));

        var summary = await CreateService().ImportCsvAsync(workspaceId, Guid.NewGuid(), MakeUpload(csv));

        Assert.Equal(2, summary.Rows);
        Assert.Equal(1, summary.Schemas);
        Assert.Equal(2, summary.Tables);
        Assert.Equal(2, summary.ColumnsCreated);
        Assert.Equal(1, summary.ColumnsUpdated);
    }

    [Fact]
    public async Task ImportCsvAsync_TrimsWhitespaceAroundValues()
    {
        var workspaceId = Guid.NewGuid();
        var schemaId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var csv = "schema_name,table_name,column_name,data_type\n  sales , orders ,  id  , uuid ";

        SetupUpsertChain(workspaceId,
            new Dictionary<string, Guid> { ["sales"] = schemaId },
            new Dictionary<(Guid SchemaId, string Name), Guid> { [(schemaId, "orders")] = tableId });

        IReadOnlyCollection<ColumnImport>? imported = null;
        _columnRepo.Setup(r => r.UpsertManyAsync(workspaceId, It.IsAny<IReadOnlyCollection<ColumnImport>>()))
            .Callback<Guid, IReadOnlyCollection<ColumnImport>>((_, c) => imported = c)
            .ReturnsAsync(new ColumnUpsertResult(1, 0, false));

        await CreateService().ImportCsvAsync(workspaceId, Guid.NewGuid(), MakeUpload(csv));

        Assert.Equal("id", imported!.Single().Name);
        Assert.Equal("uuid", imported!.Single().DataType);
    }

    [Fact]
    public async Task ImportCsvAsync_EmptyFile_ThrowsValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().ImportCsvAsync(Guid.NewGuid(), Guid.NewGuid(), MakeUpload("")));
    }

    [Fact]
    public async Task ImportCsvAsync_NonCsvExtension_ThrowsValidationException()
    {
        var csv = "schema_name,table_name,column_name,data_type\nsales,orders,id,uuid";

        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().ImportCsvAsync(Guid.NewGuid(), Guid.NewGuid(), MakeUpload(csv, "columns.xlsx")));
    }

    [Fact]
    public async Task ImportCsvAsync_WrongHeaders_ThrowsValidationExceptionNotUnhandled()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().ImportCsvAsync(Guid.NewGuid(), Guid.NewGuid(), MakeUpload("foo,bar\n1,2")));

        Assert.Contains("schema_name", ex.Message);
    }

    [Fact]
    public async Task ImportCsvAsync_HeaderOnly_ThrowsValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().ImportCsvAsync(Guid.NewGuid(), Guid.NewGuid(),
                MakeUpload("schema_name,table_name,column_name,data_type")));
    }

    [Fact]
    public async Task ImportCsvAsync_BlankRequiredField_ThrowsValidationExceptionNamingTheRow()
    {
        var csv = "schema_name,table_name,column_name,data_type\nsales,orders,,uuid";

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().ImportCsvAsync(Guid.NewGuid(), Guid.NewGuid(), MakeUpload(csv)));

        Assert.Contains("row 2", ex.Message);
        Assert.Contains("column_name", ex.Message);
    }

    [Fact]
    public async Task ImportCsvAsync_InvalidRows_WritesNothing()
    {
        var csv = "schema_name,table_name,column_name,data_type\nsales,orders,,uuid";

        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().ImportCsvAsync(Guid.NewGuid(), Guid.NewGuid(), MakeUpload(csv)));

        _schemaRepo.Verify(r => r.UpsertManyAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<string>>()), Times.Never);
        _columnRepo.Verify(r => r.UpsertManyAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<ColumnImport>>()), Times.Never);
        _projectionService.Verify(p => p.RefreshAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task ImportCsvAsync_ConcurrentEditConflict_ThrowsVersionConflictException()
    {
        var workspaceId = Guid.NewGuid();
        var schemaId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var csv = "schema_name,table_name,column_name,data_type\nsales,orders,id,uuid";

        SetupUpsertChain(workspaceId,
            new Dictionary<string, Guid> { ["sales"] = schemaId },
            new Dictionary<(Guid SchemaId, string Name), Guid> { [(schemaId, "orders")] = tableId });
        _columnRepo.Setup(r => r.UpsertManyAsync(workspaceId, It.IsAny<IReadOnlyCollection<ColumnImport>>()))
            .ReturnsAsync(new ColumnUpsertResult(0, 0, true));

        await Assert.ThrowsAsync<VersionConflictException>(() =>
            CreateService().ImportCsvAsync(workspaceId, Guid.NewGuid(), MakeUpload(csv)));

        _projectionService.Verify(p => p.RefreshAsync(It.IsAny<Guid>()), Times.Never);
    }
}
