using DataMap.Api.DTOs;
using DataMap.Api.Exceptions;
using DataMap.Tests.Integration;
using Moq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DataMap.Tests.Integration.Endpoints;

public class BusinessTermEndpointTests(TestFixture fixture) : IClassFixture<TestFixture>
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    // ── Auth guard ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTerms_NoSession_Returns401()
    {
        var client = fixture.CreateClient();
        var response = await client.GetAsync("/business-terms");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateTerm_NoSession_Returns401()
    {
        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/business-terms", new { name = "Revenue", definition = "" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MapTerm_NoSession_Returns401()
    {
        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/business-terms/map",
            new { termId = Guid.NewGuid(), columnId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── GET /business-terms ──────────────────────────────────────────────────

    [Fact]
    public async Task GetTerms_AuthenticatedRequest_Returns200WithTerms()
    {
        var terms = new List<BusinessTermDto>
        {
            new(Guid.NewGuid(), "Customer", "A paying customer"),
            new(Guid.NewGuid(), "Revenue",  "Income generated")
        };
        fixture.BusinessTermService.Setup(s => s.GetAllAsync(TestFixture.TestWorkspaceId)).ReturnsAsync(terms);

        var client = fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync("/business-terms");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<BusinessTermDto>>(JsonOpts);
        Assert.Equal(2, result!.Count);
        Assert.Equal("Customer", result[0].Name);
    }

    [Fact]
    public async Task GetTerms_EmptyWorkspace_Returns200WithEmptyArray()
    {
        fixture.BusinessTermService.Setup(s => s.GetAllAsync(TestFixture.TestWorkspaceId)).ReturnsAsync([]);

        var client = fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync("/business-terms");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<BusinessTermDto>>(JsonOpts);
        Assert.Empty(result!);
    }

    // ── POST /business-terms ─────────────────────────────────────────────────

    [Fact]
    public async Task CreateTerm_ValidRequest_Returns201WithLocation()
    {
        var createdId = Guid.NewGuid();
        var created = new BusinessTermDto(createdId, "Revenue", "Total income");
        fixture.BusinessTermService.Setup(s => s.CreateAsync(TestFixture.TestWorkspaceId, It.IsAny<BusinessTermCreateRequest>()))
            .ReturnsAsync(created);

        var client = fixture.CreateAuthenticatedClient();
        var response = await client.PostAsJsonAsync("/business-terms", new { name = "Revenue", definition = "Total income" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains($"/business-terms/{createdId}", response.Headers.Location?.ToString() ?? "");
        var result = await response.Content.ReadFromJsonAsync<BusinessTermDto>(JsonOpts);
        Assert.Equal(createdId, result!.Id);
        Assert.Equal("Revenue", result.Name);
    }

    [Fact]
    public async Task CreateTerm_EmptyName_Returns400()
    {
        fixture.BusinessTermService.Setup(s => s.CreateAsync(TestFixture.TestWorkspaceId, It.IsAny<BusinessTermCreateRequest>()))
            .ThrowsAsync(new ValidationException("Term name is required."));

        var client = fixture.CreateAuthenticatedClient();
        var response = await client.PostAsJsonAsync("/business-terms", new { name = "", definition = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertErrorCode(await response.Content.ReadAsStringAsync(), "VALIDATION_ERROR");
    }

    // ── POST /business-terms/map ─────────────────────────────────────────────

    [Fact]
    public async Task MapTerm_ValidRequest_Returns200()
    {
        fixture.BusinessTermService.Setup(s => s.MapToColumnAsync(
            TestFixture.TestWorkspaceId, It.IsAny<TermMappingRequest>()))
            .Returns(Task.CompletedTask);

        var client = fixture.CreateAuthenticatedClient();
        var response = await client.PostAsJsonAsync("/business-terms/map",
            new { termId = Guid.NewGuid(), columnId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MapTerm_TermNotFound_Returns400()
    {
        fixture.BusinessTermService.Setup(s => s.MapToColumnAsync(
            TestFixture.TestWorkspaceId, It.IsAny<TermMappingRequest>()))
            .ThrowsAsync(new ValidationException("Business term not found."));

        var client = fixture.CreateAuthenticatedClient();
        var response = await client.PostAsJsonAsync("/business-terms/map",
            new { termId = Guid.NewGuid(), columnId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertErrorCode(await response.Content.ReadAsStringAsync(), "VALIDATION_ERROR");
    }

    [Fact]
    public async Task MapTerm_PassesWorkspaceIdFromSession()
    {
        // Clear accumulated invocations from previous tests in this class that share the fixture
        fixture.BusinessTermService.Invocations.Clear();
        fixture.BusinessTermService.Setup(s => s.MapToColumnAsync(
            TestFixture.TestWorkspaceId, It.IsAny<TermMappingRequest>()))
            .Returns(Task.CompletedTask);

        var client = fixture.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/business-terms/map",
            new { termId = Guid.NewGuid(), columnId = Guid.NewGuid() });

        fixture.BusinessTermService.Verify(s => s.MapToColumnAsync(
            TestFixture.TestWorkspaceId, It.IsAny<TermMappingRequest>()), Times.Once);
    }

    private static void AssertErrorCode(string body, string expectedCode)
    {
        var doc = JsonDocument.Parse(body);
        var code = doc.RootElement.GetProperty("error").GetProperty("code").GetString();
        Assert.Equal(expectedCode, code);
    }
}
