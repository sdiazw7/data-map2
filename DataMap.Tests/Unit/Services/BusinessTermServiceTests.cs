using DataMap.Api.DTOs;
using DataMap.Api.Exceptions;
using DataMap.Api.Models;
using DataMap.Api.Repositories;
using DataMap.Api.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace DataMap.Tests.Unit.Services;

public class BusinessTermServiceTests
{
    private readonly Mock<IBusinessTermRepository> _termRepo = new();
    private readonly Mock<IColumnRepository> _columnRepo = new();
    private readonly Mock<IProjectionService> _projectionService = new();
    private readonly Mock<ILogger<BusinessTermService>> _logger = new();

    private BusinessTermService CreateService() => new(
        _termRepo.Object,
        _columnRepo.Object,
        _projectionService.Object,
        _logger.Object);

    /// <summary>Makes the column resolve inside the given workspace.</summary>
    private void ColumnExists(Guid workspaceId, Guid columnId) =>
        _columnRepo.Setup(r => r.GetByIdAsync(workspaceId, columnId))
            .ReturnsAsync(new Column { Id = columnId, WorkspaceId = workspaceId });

    // ── GetAllAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ReturnsMappedDtos()
    {
        var workspaceId = Guid.NewGuid();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var terms = new List<BusinessTerm>
        {
            new() { Id = id1, Name = "Customer", Definition = "A paying customer" },
            new() { Id = id2, Name = "Revenue",  Definition = "Income generated"   }
        };
        _termRepo.Setup(r => r.GetAllAsync(workspaceId)).ReturnsAsync(terms);

        var result = await CreateService().GetAllAsync(workspaceId);

        Assert.Equal(2, result.Count);
        Assert.Equal(id1, result[0].Id);
        Assert.Equal("Customer", result[0].Name);
        Assert.Equal("A paying customer", result[0].Definition);
        Assert.Equal(id2, result[1].Id);
    }

    [Fact]
    public async Task GetAllAsync_EmptyWorkspace_ReturnsEmptyList()
    {
        var workspaceId = Guid.NewGuid();
        _termRepo.Setup(r => r.GetAllAsync(workspaceId)).ReturnsAsync([]);

        var result = await CreateService().GetAllAsync(workspaceId);

        Assert.Empty(result);
    }

    // ── CreateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesAndReturnsDto()
    {
        var workspaceId = Guid.NewGuid();
        var createdId = Guid.NewGuid();
        var created = new BusinessTerm { Id = createdId, Name = "Revenue", Definition = "Total income" };
        _termRepo.Setup(r => r.CreateAsync(It.IsAny<BusinessTerm>())).ReturnsAsync(created);

        var result = await CreateService().CreateAsync(workspaceId, new BusinessTermCreateRequest("Revenue", "Total income"));

        Assert.Equal(createdId, result.Id);
        Assert.Equal("Revenue", result.Name);
        Assert.Equal("Total income", result.Definition);
    }

    [Fact]
    public async Task CreateAsync_EmptyName_ThrowsValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().CreateAsync(Guid.NewGuid(), new BusinessTermCreateRequest("", "def")));
    }

    [Fact]
    public async Task CreateAsync_WhitespaceName_ThrowsValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().CreateAsync(Guid.NewGuid(), new BusinessTermCreateRequest("   ", "def")));
    }

    [Fact]
    public async Task CreateAsync_TrimsNameBeforeSaving()
    {
        var workspaceId = Guid.NewGuid();
        BusinessTerm? saved = null;
        _termRepo.Setup(r => r.CreateAsync(It.IsAny<BusinessTerm>()))
            .Callback<BusinessTerm>(t => saved = t)
            .ReturnsAsync(new BusinessTerm { Id = Guid.NewGuid(), Name = "Revenue", Definition = "" });

        await CreateService().CreateAsync(workspaceId, new BusinessTermCreateRequest("  Revenue  ", ""));

        Assert.Equal("Revenue", saved!.Name);
    }

    [Fact]
    public async Task CreateAsync_NullDefinition_DefaultsToEmptyString()
    {
        var workspaceId = Guid.NewGuid();
        BusinessTerm? saved = null;
        _termRepo.Setup(r => r.CreateAsync(It.IsAny<BusinessTerm>()))
            .Callback<BusinessTerm>(t => saved = t)
            .ReturnsAsync(new BusinessTerm { Id = Guid.NewGuid(), Name = "Revenue", Definition = "" });

        // Definition is non-nullable in the record but service guards with ?.Trim() ?? ""
        await CreateService().CreateAsync(workspaceId, new BusinessTermCreateRequest("Revenue", null!));

        Assert.Equal(string.Empty, saved!.Definition);
    }

    [Fact]
    public async Task CreateAsync_SetsWorkspaceIdOnNewTerm()
    {
        var workspaceId = Guid.NewGuid();
        BusinessTerm? saved = null;
        _termRepo.Setup(r => r.CreateAsync(It.IsAny<BusinessTerm>()))
            .Callback<BusinessTerm>(t => saved = t)
            .ReturnsAsync(new BusinessTerm { Id = Guid.NewGuid(), Name = "X", Definition = "" });

        await CreateService().CreateAsync(workspaceId, new BusinessTermCreateRequest("X", ""));

        Assert.Equal(workspaceId, saved!.WorkspaceId);
    }

    // ── MapToColumnAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task MapToColumnAsync_ValidRequest_CreatesMapping()
    {
        var workspaceId = Guid.NewGuid();
        var termId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        var term = new BusinessTerm { Id = termId, WorkspaceId = workspaceId };

        _termRepo.Setup(r => r.GetByIdAsync(termId)).ReturnsAsync(term);
        ColumnExists(workspaceId, columnId);
        _termRepo.Setup(r => r.GetMappingByColumnAsync(columnId)).ReturnsAsync((TermColumnMapping?)null);
        _termRepo.Setup(r => r.MapTermToColumnAsync(It.IsAny<TermColumnMapping>()))
            .ReturnsAsync(new TermColumnMapping { Id = Guid.NewGuid(), TermId = termId, ColumnId = columnId });

        await CreateService().MapToColumnAsync(workspaceId, new TermMappingRequest(termId, columnId));

        _termRepo.Verify(r => r.MapTermToColumnAsync(It.Is<TermColumnMapping>(
            m => m.TermId == termId && m.ColumnId == columnId)), Times.Once);
    }

    [Fact]
    public async Task MapToColumnAsync_ColumnAlreadyMapped_ReplacesExistingMappingInsteadOfInserting()
    {
        var workspaceId = Guid.NewGuid();
        var termId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        var existing = new TermColumnMapping { Id = Guid.NewGuid(), TermId = Guid.NewGuid(), ColumnId = columnId };

        _termRepo.Setup(r => r.GetByIdAsync(termId)).ReturnsAsync(new BusinessTerm { Id = termId, WorkspaceId = workspaceId });
        ColumnExists(workspaceId, columnId);
        _termRepo.Setup(r => r.GetMappingByColumnAsync(columnId)).ReturnsAsync(existing);

        await CreateService().MapToColumnAsync(workspaceId, new TermMappingRequest(termId, columnId));

        // A second insert would produce two projection rows sharing one ColumnId — the
        // projection's primary key — and break every later rebuild for the workspace.
        _termRepo.Verify(r => r.MapTermToColumnAsync(It.IsAny<TermColumnMapping>()), Times.Never);
        _termRepo.Verify(r => r.UpdateMappingAsync(It.Is<TermColumnMapping>(m => m.TermId == termId)), Times.Once);
    }

    [Fact]
    public async Task MapToColumnAsync_ColumnInDifferentWorkspace_ThrowsValidationException()
    {
        var workspaceId = Guid.NewGuid();
        var termId = Guid.NewGuid();
        var columnId = Guid.NewGuid();

        _termRepo.Setup(r => r.GetByIdAsync(termId)).ReturnsAsync(new BusinessTerm { Id = termId, WorkspaceId = workspaceId });
        _columnRepo.Setup(r => r.GetByIdAsync(workspaceId, columnId)).ReturnsAsync((Column?)null);

        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().MapToColumnAsync(workspaceId, new TermMappingRequest(termId, columnId)));

        _termRepo.Verify(r => r.MapTermToColumnAsync(It.IsAny<TermColumnMapping>()), Times.Never);
    }

    [Fact]
    public async Task MapToColumnAsync_TermNotFound_ThrowsValidationException()
    {
        var workspaceId = Guid.NewGuid();
        var termId = Guid.NewGuid();
        _termRepo.Setup(r => r.GetByIdAsync(termId)).ReturnsAsync((BusinessTerm?)null);

        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().MapToColumnAsync(workspaceId, new TermMappingRequest(termId, Guid.NewGuid())));
    }

    [Fact]
    public async Task MapToColumnAsync_TermBelongsToDifferentWorkspace_ThrowsValidationException()
    {
        var workspaceId = Guid.NewGuid();
        var termId = Guid.NewGuid();
        var term = new BusinessTerm { Id = termId, WorkspaceId = Guid.NewGuid() }; // different workspace

        _termRepo.Setup(r => r.GetByIdAsync(termId)).ReturnsAsync(term);

        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().MapToColumnAsync(workspaceId, new TermMappingRequest(termId, Guid.NewGuid())));
    }

    [Fact]
    public async Task MapToColumnAsync_SyncsOnlyTheAffectedProjectionRow()
    {
        var workspaceId = Guid.NewGuid();
        var termId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        var term = new BusinessTerm { Id = termId, WorkspaceId = workspaceId, Name = "Revenue" };

        _termRepo.Setup(r => r.GetByIdAsync(termId)).ReturnsAsync(term);
        ColumnExists(workspaceId, columnId);
        _termRepo.Setup(r => r.GetMappingByColumnAsync(columnId)).ReturnsAsync((TermColumnMapping?)null);
        _termRepo.Setup(r => r.MapTermToColumnAsync(It.IsAny<TermColumnMapping>()))
            .ReturnsAsync(new TermColumnMapping { Id = Guid.NewGuid() });

        await CreateService().MapToColumnAsync(workspaceId, new TermMappingRequest(termId, columnId));

        _projectionService.Verify(p => p.SyncColumnTermAsync(workspaceId, columnId, "Revenue"), Times.Once);
        _projectionService.Verify(p => p.RefreshAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task MapToColumnAsync_DoesNotTouchProjectionOnValidationFailure()
    {
        var workspaceId = Guid.NewGuid();
        var termId = Guid.NewGuid();
        _termRepo.Setup(r => r.GetByIdAsync(termId)).ReturnsAsync((BusinessTerm?)null);

        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().MapToColumnAsync(workspaceId, new TermMappingRequest(termId, Guid.NewGuid())));

        _projectionService.Verify(p => p.RefreshAsync(It.IsAny<Guid>()), Times.Never);
        _projectionService.Verify(p => p.SyncColumnTermAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string?>()), Times.Never);
    }
}
