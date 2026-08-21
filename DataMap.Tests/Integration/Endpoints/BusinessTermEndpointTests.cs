using DataMap.Api.DTOs;
using DataMap.Api.Exceptions;
using DataMap.Tests.Integration;
using Moq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace DataMap.Tests.Integration.Endpoints;

public class BusinessTermEndpointTests(TestFixture fixture) : IClassFixture<TestFixture>
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private static PagedResult<BusinessTermDto> Page(List<BusinessTermDto> items, int? total = null)
        => new(items, total ?? items.Count, 200, 0);

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
    public async Task SetColumnTerm_NoSession_Returns401()
    {
        var client = fixture.CreateClient();
        var response = await client.PutAsJsonAsync($"/columns/{Guid.NewGuid()}/business-term",
            new { termId = Guid.NewGuid() });

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
        fixture.BusinessTermService.Setup(s => s.GetAllAsync(TestFixture.TestWorkspaceId, It.IsAny<PageQuery>()))
            .ReturnsAsync(Page(terms, total: 2));

        var client = fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync("/business-terms");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<BusinessTermDto>>(JsonOpts);
        Assert.Equal(2, result!.Items.Count);
        Assert.Equal(2, result.Total);
        Assert.Equal("Customer", result.Items[0].Name);
    }

    [Fact]
    public async Task GetTerms_PassesPagingToService()
    {
        fixture.BusinessTermService.Setup(s => s.GetAllAsync(
            TestFixture.TestWorkspaceId,
            It.Is<PageQuery>(p => p.Limit == 25 && p.Offset == 50)))
            .ReturnsAsync(Page([]));

        var client = fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync("/business-terms?limit=25&offset=50");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        fixture.BusinessTermService.Verify(s => s.GetAllAsync(
            TestFixture.TestWorkspaceId,
            It.Is<PageQuery>(p => p.Limit == 25 && p.Offset == 50)), Times.Once);
    }

    [Fact]
    public async Task GetTerms_EmptyWorkspace_Returns200WithEmptyItems()
    {
        fixture.BusinessTermService.Setup(s => s.GetAllAsync(TestFixture.TestWorkspaceId, It.IsAny<PageQuery>()))
            .ReturnsAsync(Page([], total: 0));

        var client = fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync("/business-terms");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<BusinessTermDto>>(JsonOpts);
        Assert.Empty(result!.Items);
    }

    // ── GET /business-terms/{id} ─────────────────────────────────────────────

    [Fact]
    public async Task GetTermById_Found_Returns200()
    {
        var id = Guid.NewGuid();
        fixture.BusinessTermService.Setup(s => s.GetByIdAsync(TestFixture.TestWorkspaceId, id))
            .ReturnsAsync(new BusinessTermDto(id, "Revenue", "Total income"));

        var client = fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync($"/business-terms/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<BusinessTermDto>(JsonOpts);
        Assert.Equal(id, result!.Id);
    }

    [Fact]
    public async Task GetTermById_NotFound_Returns404()
    {
        var id = Guid.NewGuid();
        fixture.BusinessTermService.Setup(s => s.GetByIdAsync(TestFixture.TestWorkspaceId, id))
            .ThrowsAsync(new BusinessTermNotFoundException());

        var client = fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync($"/business-terms/{id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertErrorCode(await response.Content.ReadAsStringAsync(), "BUSINESS_TERM_NOT_FOUND");
    }

    [Fact]
    public async Task GetTermById_NonGuidId_Returns404RatherThanMatchingTheRoute()
    {
        // The :guid route constraint keeps a junk id from ever reaching the handler.
        var client = fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync("/business-terms/not-a-guid");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertErrorCode(await response.Content.ReadAsStringAsync(), "NOT_FOUND");
    }

    // ── POST /business-terms ─────────────────────────────────────────────────

    [Fact]
    public async Task CreateTerm_ValidRequest_Returns201WithResolvableLocation()
    {
        var createdId = Guid.NewGuid();
        var created = new BusinessTermDto(createdId, "Revenue", "Total income");
        fixture.BusinessTermService.Setup(s => s.CreateAsync(TestFixture.TestWorkspaceId, It.IsAny<BusinessTermCreateRequest>()))
            .ReturnsAsync(created);
        fixture.BusinessTermService.Setup(s => s.GetByIdAsync(TestFixture.TestWorkspaceId, createdId))
            .ReturnsAsync(created);

        var client = fixture.CreateAuthenticatedClient();
        var response = await client.PostAsJsonAsync("/business-terms", new { name = "Revenue", definition = "Total income" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var location = response.Headers.Location?.ToString() ?? "";
        Assert.Contains($"/business-terms/{createdId}", location);

        var result = await response.Content.ReadFromJsonAsync<BusinessTermDto>(JsonOpts);
        Assert.Equal(createdId, result!.Id);
        Assert.Equal("Revenue", result.Name);

        // The Location used to point at a route that was never mapped. Following it must work.
        var followed = await client.GetAsync(location);
        Assert.Equal(HttpStatusCode.OK, followed.StatusCode);
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

    [Fact]
    public async Task CreateTerm_DuplicateName_Returns409()
    {
        fixture.BusinessTermService.Setup(s => s.CreateAsync(TestFixture.TestWorkspaceId, It.IsAny<BusinessTermCreateRequest>()))
            .ThrowsAsync(new BusinessTermAlreadyExistsException("Revenue"));

        var client = fixture.CreateAuthenticatedClient();
        var response = await client.PostAsJsonAsync("/business-terms", new { name = "Revenue", definition = "" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        AssertErrorCode(await response.Content.ReadAsStringAsync(), "BUSINESS_TERM_ALREADY_EXISTS");
    }

    // ── PUT /columns/{columnId}/business-term ────────────────────────────────

    [Fact]
    public async Task SetColumnTerm_ValidRequest_Returns204()
    {
        var columnId = Guid.NewGuid();
        var termId = Guid.NewGuid();
        fixture.BusinessTermService.Setup(s => s.MapToColumnAsync(TestFixture.TestWorkspaceId, columnId, termId))
            .Returns(Task.CompletedTask);

        var client = fixture.CreateAuthenticatedClient();
        var response = await client.PutAsJsonAsync($"/columns/{columnId}/business-term", new { termId });

        // 204, not a bodyless 200 — there is nothing to return and the status should say so.
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, (await response.Content.ReadAsByteArrayAsync()).Length);
    }

    [Fact]
    public async Task SetColumnTerm_TakesColumnFromRouteAndTermFromBody()
    {
        fixture.BusinessTermService.Invocations.Clear();

        var columnId = Guid.NewGuid();
        var termId = Guid.NewGuid();
        fixture.BusinessTermService.Setup(s => s.MapToColumnAsync(TestFixture.TestWorkspaceId, columnId, termId))
            .Returns(Task.CompletedTask);

        var client = fixture.CreateAuthenticatedClient();
        await client.PutAsJsonAsync($"/columns/{columnId}/business-term", new { termId });

        fixture.BusinessTermService.Verify(s => s.MapToColumnAsync(
            TestFixture.TestWorkspaceId, columnId, termId), Times.Once);
    }

    [Fact]
    public async Task SetColumnTerm_TermNotFound_Returns404()
    {
        var columnId = Guid.NewGuid();
        fixture.BusinessTermService.Setup(s => s.MapToColumnAsync(
            TestFixture.TestWorkspaceId, columnId, It.IsAny<Guid>()))
            .ThrowsAsync(new BusinessTermNotFoundException());

        var client = fixture.CreateAuthenticatedClient();
        var response = await client.PutAsJsonAsync($"/columns/{columnId}/business-term",
            new { termId = Guid.NewGuid() });

        // 404 rather than the 400 this used to report — the referenced term does not exist.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertErrorCode(await response.Content.ReadAsStringAsync(), "BUSINESS_TERM_NOT_FOUND");
    }

    [Fact]
    public async Task SetColumnTerm_ColumnNotFound_Returns404()
    {
        var columnId = Guid.NewGuid();
        fixture.BusinessTermService.Setup(s => s.MapToColumnAsync(
            TestFixture.TestWorkspaceId, columnId, It.IsAny<Guid>()))
            .ThrowsAsync(new ColumnNotFoundException());

        var client = fixture.CreateAuthenticatedClient();
        var response = await client.PutAsJsonAsync($"/columns/{columnId}/business-term",
            new { termId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertErrorCode(await response.Content.ReadAsStringAsync(), "COLUMN_NOT_FOUND");
    }

    // ── DELETE /columns/{columnId}/business-term ─────────────────────────────

    [Fact]
    public async Task ClearColumnTerm_Returns204()
    {
        var columnId = Guid.NewGuid();
        fixture.BusinessTermService.Setup(s => s.UnmapFromColumnAsync(TestFixture.TestWorkspaceId, columnId))
            .Returns(Task.CompletedTask);

        var client = fixture.CreateAuthenticatedClient();
        var response = await client.DeleteAsync($"/columns/{columnId}/business-term");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task ClearColumnTerm_CallsServiceWithSessionWorkspace()
    {
        fixture.BusinessTermService.Invocations.Clear();

        var columnId = Guid.NewGuid();
        fixture.BusinessTermService.Setup(s => s.UnmapFromColumnAsync(TestFixture.TestWorkspaceId, columnId))
            .Returns(Task.CompletedTask);

        var client = fixture.CreateAuthenticatedClient();
        await client.DeleteAsync($"/columns/{columnId}/business-term");

        fixture.BusinessTermService.Verify(s => s.UnmapFromColumnAsync(
            TestFixture.TestWorkspaceId, columnId), Times.Once);
    }

    [Fact]
    public async Task ClearColumnTerm_ColumnNotFound_Returns404()
    {
        var columnId = Guid.NewGuid();
        fixture.BusinessTermService.Setup(s => s.UnmapFromColumnAsync(TestFixture.TestWorkspaceId, columnId))
            .ThrowsAsync(new ColumnNotFoundException());

        var client = fixture.CreateAuthenticatedClient();
        var response = await client.DeleteAsync($"/columns/{columnId}/business-term");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertErrorCode(await response.Content.ReadAsStringAsync(), "COLUMN_NOT_FOUND");
    }

    [Fact]
    public async Task OldMapRoute_NoLongerAccepted()
    {
        var client = fixture.CreateAuthenticatedClient();
        var response = await client.PostAsync("/business-terms/map",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        // 405 rather than 404: routing rejects the method against the /business-terms/{id}
        // template before it evaluates that template's :guid constraint. Either way the verb
        // endpoint is gone, and the reply carries the standard envelope.
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
