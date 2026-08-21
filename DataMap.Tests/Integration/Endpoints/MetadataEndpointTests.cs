using DataMap.Api.DTOs;
using DataMap.Api.Exceptions;
using DataMap.Api.Services;
using DataMap.Tests.Integration;
using Moq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace DataMap.Tests.Integration.Endpoints;

public class MetadataEndpointTests(TestFixture fixture) : IClassFixture<TestFixture>
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private static PagedResult<ColumnGridRow> Page(List<ColumnGridRow> items, int? total = null)
        => new(items, total ?? items.Count, 200, 0);

    // ── Auth guard ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetColumns_NoSession_Returns401()
    {
        var client = fixture.CreateClient();
        var response = await client.GetAsync("/columns");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCoverage_NoSession_Returns401()
    {
        var client = fixture.CreateClient();
        var response = await client.GetAsync("/coverage");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetTables_NoSession_Returns401()
    {
        var client = fixture.CreateClient();
        var response = await client.GetAsync("/tables");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PatchColumns_NoSession_Returns401()
    {
        var client = fixture.CreateClient();
        var response = await client.PatchAsync("/columns",
            new StringContent("[]", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── GET /columns ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetColumns_AuthenticatedRequest_Returns200WithColumns()
    {
        var columns = new List<ColumnGridRow>
        {
            new(Guid.NewGuid(), "sales", "orders", "id", "uuid", null, null, null, null, 1),
            new(Guid.NewGuid(), "sales", "orders", "total", "numeric", null, null, null, null, 1)
        };
        fixture.MetadataService.Setup(s => s.GetColumnsAsync(TestFixture.TestWorkspaceId, It.IsAny<MetadataColumnsQuery>()))
            .ReturnsAsync(Page(columns, total: 2));

        var client = fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync("/columns");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ColumnGridRow>>(JsonOpts);
        Assert.Equal(2, result!.Items.Count);
    }

    [Fact]
    public async Task GetColumns_ReportsTotalLimitAndOffsetAlongsideItems()
    {
        // The whole point of the envelope: a caller on page one can tell there are more pages
        // without walking to the end of the catalog to find out.
        var columns = new List<ColumnGridRow>
        {
            new(Guid.NewGuid(), "sales", "orders", "id", "uuid", null, null, null, null, 1)
        };
        fixture.MetadataService.Setup(s => s.GetColumnsAsync(TestFixture.TestWorkspaceId, It.IsAny<MetadataColumnsQuery>()))
            .ReturnsAsync(new PagedResult<ColumnGridRow>(columns, Total: 104318, Limit: 1, Offset: 40));

        var client = fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync("/columns?limit=1&offset=40");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ColumnGridRow>>(JsonOpts);
        Assert.Single(result!.Items);
        Assert.Equal(104318, result.Total);
        Assert.Equal(1, result.Limit);
        Assert.Equal(40, result.Offset);
    }

    [Fact]
    public async Task GetColumns_WithSearchQueryParam_PassesSearchToService()
    {
        fixture.MetadataService.Setup(s => s.GetColumnsAsync(
            TestFixture.TestWorkspaceId,
            It.Is<MetadataColumnsQuery>(q => q.Search == "customer")))
            .ReturnsAsync(Page([]));

        var client = fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync("/columns?search=customer");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        fixture.MetadataService.Verify(s => s.GetColumnsAsync(
            TestFixture.TestWorkspaceId,
            It.Is<MetadataColumnsQuery>(q => q.Search == "customer")), Times.Once);
    }

    [Fact]
    public async Task GetColumns_WithUndocumentedOnlyParam_PassesFlagToService()
    {
        fixture.MetadataService.Setup(s => s.GetColumnsAsync(
            TestFixture.TestWorkspaceId,
            It.Is<MetadataColumnsQuery>(q => q.UndocumentedOnly)))
            .ReturnsAsync(Page([]));

        var client = fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync("/columns?undocumentedOnly=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        fixture.MetadataService.Verify(s => s.GetColumnsAsync(
            TestFixture.TestWorkspaceId,
            It.Is<MetadataColumnsQuery>(q => q.UndocumentedOnly)), Times.Once);
    }

    [Fact]
    public async Task GetColumns_WithPagingAndSortParams_PassesThemToService()
    {
        fixture.MetadataService.Setup(s => s.GetColumnsAsync(
            TestFixture.TestWorkspaceId,
            It.Is<MetadataColumnsQuery>(q =>
                q.Limit == 50 && q.Offset == 100 && q.SortBy == "tableName" && q.SortDir == "desc"
                && q.TableName == "orders")))
            .ReturnsAsync(Page([]));

        var client = fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync(
            "/columns?limit=50&offset=100&sortBy=tableName&sortDir=desc&tableName=orders");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        fixture.MetadataService.Verify(s => s.GetColumnsAsync(
            TestFixture.TestWorkspaceId,
            It.Is<MetadataColumnsQuery>(q =>
                q.Limit == 50 && q.Offset == 100 && q.SortBy == "tableName" && q.SortDir == "desc"
                && q.TableName == "orders")), Times.Once);
    }

    [Fact]
    public async Task GetColumns_EmptyResult_Returns200WithEmptyItems()
    {
        fixture.MetadataService.Setup(s => s.GetColumnsAsync(TestFixture.TestWorkspaceId, It.IsAny<MetadataColumnsQuery>()))
            .ReturnsAsync(Page([], total: 0));

        var client = fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync("/columns");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ColumnGridRow>>(JsonOpts);
        Assert.Empty(result!.Items);
        Assert.Equal(0, result.Total);
    }

    // ── GET /tables ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTables_AuthenticatedRequest_Returns200WithPagedNames()
    {
        fixture.MetadataService.Setup(s => s.GetTableNamesAsync(TestFixture.TestWorkspaceId, It.IsAny<PageQuery>()))
            .ReturnsAsync(new PagedResult<string>(["customers", "orders"], 2, 500, 0));

        var client = fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync("/tables");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<string>>(JsonOpts);
        Assert.Equal(2, result!.Items.Count);
        Assert.Equal(2, result.Total);
    }

    // ── PATCH /columns ───────────────────────────────────────────────────────

    [Fact]
    public async Task PatchColumns_ValidUpdates_Returns200WithNewVersions()
    {
        var columnId = Guid.NewGuid();
        fixture.MetadataService.Setup(s => s.BulkUpdateAsync(
            TestFixture.TestWorkspaceId,
            TestFixture.TestParticipantId,
            It.IsAny<List<ColumnUpdateRequest>>()))
            .ReturnsAsync(new BulkUpdateResponse([new ColumnVersionDto(columnId, 2)]));

        var client = fixture.CreateAuthenticatedClient();
        var updates = new[] { new { columnId, description = "A column", exampleValue = (string?)null, owner = (string?)null, version = 1 } };
        var response = await client.PatchAsync("/columns",
            new StringContent(JsonSerializer.Serialize(updates), Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Without these the client has to refetch the grid just to learn the numbers it needs
        // for its next optimistic write.
        var result = await response.Content.ReadFromJsonAsync<BulkUpdateResponse>(JsonOpts);
        var updated = Assert.Single(result!.Columns);
        Assert.Equal(columnId, updated.ColumnId);
        Assert.Equal(2, updated.Version);
    }

    [Fact]
    public async Task PatchColumns_VersionConflict_Returns409()
    {
        fixture.MetadataService.Setup(s => s.BulkUpdateAsync(
            TestFixture.TestWorkspaceId,
            TestFixture.TestParticipantId,
            It.IsAny<List<ColumnUpdateRequest>>()))
            .ThrowsAsync(new VersionConflictException());

        var client = fixture.CreateAuthenticatedClient();
        var updates = new[] { new { columnId = Guid.NewGuid(), description = "desc", exampleValue = (string?)null, owner = (string?)null, version = 1 } };
        var response = await client.PatchAsync("/columns",
            new StringContent(JsonSerializer.Serialize(updates), Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        AssertErrorCode(await response.Content.ReadAsStringAsync(), "VERSION_CONFLICT");
    }

    [Fact]
    public async Task PatchColumns_MalformedJsonBody_Returns400InTheStandardErrorShape()
    {
        // Model binding fails before the handler runs. This used to escape the custom envelope
        // and surface as the framework's ProblemDetails, which no client of this API reads.
        var client = fixture.CreateAuthenticatedClient();
        var response = await client.PatchAsync("/columns",
            new StringContent("{ not json", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertErrorCode(await response.Content.ReadAsStringAsync(), "MALFORMED_REQUEST");
    }

    // ── GET /coverage ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCoverage_AuthenticatedRequest_Returns200WithCoverageData()
    {
        fixture.MetadataService.Setup(s => s.GetCoverageAsync(TestFixture.TestWorkspaceId))
            .ReturnsAsync(new CoverageResponse(100, 75, 75.0));

        var client = fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync("/coverage");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CoverageResponse>(JsonOpts);
        Assert.Equal(100, result!.TotalColumns);
        Assert.Equal(75, result.DocumentedColumns);
        Assert.Equal(75.0, result.CoveragePercent);
    }

    // ── POST /imports ────────────────────────────────────────────────────────

    [Fact]
    public async Task ImportCsv_NoSession_Returns401()
    {
        var client = fixture.CreateClient();
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("schema_name,table_name,column_name,data_type"), "file", "test.csv");

        var response = await client.PostAsync("/imports", content);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ImportCsv_ValidFile_Returns200WithSummary()
    {
        fixture.MetadataImportService.Setup(s => s.ImportCsvAsync(
            TestFixture.TestWorkspaceId,
            TestFixture.TestParticipantId,
            It.IsAny<CsvUpload>()))
            .ReturnsAsync(new ImportSummary(1, 1, 1, 1, 0));

        var client = fixture.CreateAuthenticatedClient();
        using var content = new MultipartFormDataContent();
        var csvBytes = Encoding.UTF8.GetBytes("schema_name,table_name,column_name,data_type\nsales,orders,id,uuid");
        content.Add(new ByteArrayContent(csvBytes) { Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv") } }, "file", "test.csv");

        var response = await client.PostAsync("/imports", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ImportSummary>(JsonOpts);
        Assert.Equal(1, result!.Rows);
        Assert.Equal(1, result.ColumnsCreated);
    }

    [Fact]
    public async Task ImportCsv_NoFilePart_Returns400InTheStandardErrorShape()
    {
        var client = fixture.CreateAuthenticatedClient();
        using var content = new MultipartFormDataContent();
        // Include a non-file field so form parsing succeeds, but "file" is absent
        content.Add(new StringContent("some_value"), "other_field");
        var response = await client.PostAsync("/imports", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Previously hand-built at the endpoint with its own code; now the same envelope as
        // every other validation failure.
        AssertErrorCode(await response.Content.ReadAsStringAsync(), "VALIDATION_ERROR");
    }

    // ── Framework-generated failures ─────────────────────────────────────────

    [Fact]
    public async Task UnmatchedRoute_Returns404InTheStandardErrorShape()
    {
        var client = fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync("/no-such-route");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertErrorCode(await response.Content.ReadAsStringAsync(), "NOT_FOUND");
    }

    [Fact]
    public async Task WrongMethodOnKnownRoute_Returns405InTheStandardErrorShape()
    {
        var client = fixture.CreateAuthenticatedClient();
        var response = await client.PostAsync("/coverage", new StringContent("", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        AssertErrorCode(await response.Content.ReadAsStringAsync(), "METHOD_NOT_ALLOWED");
    }

    private static void AssertErrorCode(string body, string expectedCode)
    {
        var doc = JsonDocument.Parse(body);
        var code = doc.RootElement.GetProperty("error").GetProperty("code").GetString();
        Assert.Equal(expectedCode, code);
    }
}
