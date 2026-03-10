using DataMap.Api.DTOs;
using DataMap.Api.Exceptions;
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

    // ── Auth guard ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetColumns_NoSession_Returns401()
    {
        var client = fixture.CreateClient();
        var response = await client.GetAsync("/metadata/columns");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCoverage_NoSession_Returns401()
    {
        var client = fixture.CreateClient();
        var response = await client.GetAsync("/metadata/coverage");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PatchColumns_NoSession_Returns401()
    {
        var client = fixture.CreateClient();
        var response = await client.PatchAsync("/metadata/columns",
            new StringContent("[]", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── GET /metadata/columns ────────────────────────────────────────────────

    [Fact]
    public async Task GetColumns_AuthenticatedRequest_Returns200WithColumns()
    {
        var columns = new List<ColumnGridRow>
        {
            new(Guid.NewGuid(), "sales", "orders", "id", "uuid", null, null, null, null, 1),
            new(Guid.NewGuid(), "sales", "orders", "total", "numeric", null, null, null, null, 1)
        };
        fixture.MetadataService.Setup(s => s.GetColumnsAsync(TestFixture.TestWorkspaceId, It.IsAny<MetadataColumnsQuery>()))
            .ReturnsAsync(columns);

        var client = fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync("/metadata/columns");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<ColumnGridRow>>(JsonOpts);
        Assert.Equal(2, result!.Count);
    }

    [Fact]
    public async Task GetColumns_WithSearchQueryParam_PassesSearchToService()
    {
        fixture.MetadataService.Setup(s => s.GetColumnsAsync(
            TestFixture.TestWorkspaceId,
            It.Is<MetadataColumnsQuery>(q => q.Search == "customer")))
            .ReturnsAsync([]);

        var client = fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync("/metadata/columns?search=customer");

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
            .ReturnsAsync([]);

        var client = fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync("/metadata/columns?undocumented_only=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        fixture.MetadataService.Verify(s => s.GetColumnsAsync(
            TestFixture.TestWorkspaceId,
            It.Is<MetadataColumnsQuery>(q => q.UndocumentedOnly)), Times.Once);
    }

    [Fact]
    public async Task GetColumns_EmptyResult_Returns200WithEmptyArray()
    {
        fixture.MetadataService.Setup(s => s.GetColumnsAsync(TestFixture.TestWorkspaceId, It.IsAny<MetadataColumnsQuery>()))
            .ReturnsAsync([]);

        var client = fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync("/metadata/columns");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<ColumnGridRow>>(JsonOpts);
        Assert.Empty(result!);
    }

    // ── PATCH /metadata/columns ──────────────────────────────────────────────

    [Fact]
    public async Task PatchColumns_ValidUpdates_Returns200()
    {
        fixture.MetadataService.Setup(s => s.BulkUpdateAsync(
            TestFixture.TestWorkspaceId,
            TestFixture.TestParticipantId,
            It.IsAny<List<ColumnUpdateRequest>>()))
            .Returns(Task.CompletedTask);

        var client = fixture.CreateAuthenticatedClient();
        var updates = new[] { new { columnId = Guid.NewGuid(), description = "A column", exampleValue = (string?)null, owner = (string?)null, version = 1 } };
        var response = await client.PatchAsync("/metadata/columns",
            new StringContent(JsonSerializer.Serialize(updates), Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
        var response = await client.PatchAsync("/metadata/columns",
            new StringContent(JsonSerializer.Serialize(updates), Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        AssertErrorCode(await response.Content.ReadAsStringAsync(), "VERSION_CONFLICT");
    }

    // ── GET /metadata/coverage ───────────────────────────────────────────────

    [Fact]
    public async Task GetCoverage_AuthenticatedRequest_Returns200WithCoverageData()
    {
        fixture.MetadataService.Setup(s => s.GetCoverageAsync(TestFixture.TestWorkspaceId))
            .ReturnsAsync(new CoverageResponse(100, 75, 75.0));

        var client = fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync("/metadata/coverage");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CoverageResponse>(JsonOpts);
        Assert.Equal(100, result!.TotalColumns);
        Assert.Equal(75, result.DocumentedColumns);
        Assert.Equal(75.0, result.CoveragePercent);
    }

    // ── POST /metadata/upload ────────────────────────────────────────────────

    [Fact]
    public async Task UploadCsv_NoSession_Returns401()
    {
        var client = fixture.CreateClient();
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("schema_name,table_name,column_name,data_type"), "file", "test.csv");

        var response = await client.PostAsync("/metadata/upload", content);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UploadCsv_ValidFile_Returns200()
    {
        fixture.MetadataService.Setup(s => s.UploadCsvAsync(
            TestFixture.TestWorkspaceId,
            TestFixture.TestParticipantId,
            It.IsAny<Microsoft.AspNetCore.Http.IFormFile>()))
            .Returns(Task.CompletedTask);

        var client = fixture.CreateAuthenticatedClient();
        using var content = new MultipartFormDataContent();
        var csvBytes = Encoding.UTF8.GetBytes("schema_name,table_name,column_name,data_type\nsales,orders,id,uuid");
        content.Add(new ByteArrayContent(csvBytes) { Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv") } }, "file", "test.csv");

        var response = await client.PostAsync("/metadata/upload", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UploadCsv_NoFilePart_Returns400()
    {
        var client = fixture.CreateAuthenticatedClient();
        using var content = new MultipartFormDataContent();
        // Include a non-file field so form parsing succeeds, but "file" is absent
        content.Add(new StringContent("some_value"), "other_field");
        var response = await client.PostAsync("/metadata/upload", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static void AssertErrorCode(string body, string expectedCode)
    {
        var doc = JsonDocument.Parse(body);
        var code = doc.RootElement.GetProperty("error").GetProperty("code").GetString();
        Assert.Equal(expectedCode, code);
    }
}
