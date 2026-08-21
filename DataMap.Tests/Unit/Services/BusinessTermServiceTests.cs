using DataMap.Api.Data;
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
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<BusinessTermService>> _logger = new();

    public BusinessTermServiceTests()
    {
        // Run the transactional body inline; what matters is which calls happen inside it.
        _unitOfWork.Setup(u => u.ExecuteAsync(It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(operation => operation());
    }

    private BusinessTermService CreateService() => new(
        _termRepo.Object,
        _columnRepo.Object,
        _projectionService.Object,
        _unitOfWork.Object,
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
        _termRepo.Setup(r => r.GetAllAsync(workspaceId, It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync((terms, 2));

        var result = await CreateService().GetAllAsync(workspaceId, new PageQuery(200, 0));

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(2, result.Total);
        Assert.Equal(id1, result.Items[0].Id);
        Assert.Equal("Customer", result.Items[0].Name);
        Assert.Equal("A paying customer", result.Items[0].Definition);
        Assert.Equal(id2, result.Items[1].Id);
    }

    [Fact]
    public async Task GetAllAsync_EmptyWorkspace_ReturnsEmptyList()
    {
        var workspaceId = Guid.NewGuid();
        _termRepo.Setup(r => r.GetAllAsync(workspaceId, It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((new List<BusinessTerm>(), 0));

        var result = await CreateService().GetAllAsync(workspaceId, new PageQuery(200, 0));

        Assert.Empty(result.Items);
        Assert.Equal(0, result.Total);
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

    [Fact]
    public async Task CreateAsync_DuplicateName_ThrowsBusinessTermAlreadyExists()
    {
        // (workspace_id, name) is uniquely indexed, so without this check a retyped term
        // reached the database and came back as a 500.
        var workspaceId = Guid.NewGuid();
        _termRepo.Setup(r => r.GetByNameAsync(workspaceId, "Revenue"))
            .ReturnsAsync(new BusinessTerm { Id = Guid.NewGuid(), WorkspaceId = workspaceId, Name = "Revenue" });

        await Assert.ThrowsAsync<BusinessTermAlreadyExistsException>(() =>
            CreateService().CreateAsync(workspaceId, new BusinessTermCreateRequest("Revenue", "def")));

        _termRepo.Verify(r => r.CreateAsync(It.IsAny<BusinessTerm>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_DuplicateCheckUsesTheTrimmedName()
    {
        var workspaceId = Guid.NewGuid();
        _termRepo.Setup(r => r.GetByNameAsync(workspaceId, "Revenue"))
            .ReturnsAsync(new BusinessTerm { Id = Guid.NewGuid(), Name = "Revenue" });

        await Assert.ThrowsAsync<BusinessTermAlreadyExistsException>(() =>
            CreateService().CreateAsync(workspaceId, new BusinessTermCreateRequest("  Revenue  ", "def")));
    }

    // ── MapToColumnAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task MapToColumnAsync_ValidRequest_SetsBusinessTermOnColumn()
    {
        var workspaceId = Guid.NewGuid();
        var termId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        var term = new BusinessTerm { Id = termId, WorkspaceId = workspaceId };

        _termRepo.Setup(r => r.GetByIdAsync(termId)).ReturnsAsync(term);
        ColumnExists(workspaceId, columnId);

        await CreateService().MapToColumnAsync(workspaceId, columnId, termId);

        _columnRepo.Verify(r => r.SetBusinessTermAsync(workspaceId, columnId, termId), Times.Once);
    }

    [Fact]
    public async Task MapToColumnAsync_ColumnAlreadyMapped_ReplacesExistingTerm()
    {
        var workspaceId = Guid.NewGuid();
        var termId = Guid.NewGuid();
        var columnId = Guid.NewGuid();

        _termRepo.Setup(r => r.GetByIdAsync(termId)).ReturnsAsync(new BusinessTerm { Id = termId, WorkspaceId = workspaceId });
        ColumnExists(workspaceId, columnId);

        await CreateService().MapToColumnAsync(workspaceId, columnId, termId);

        // A column holds at most one term, so remapping is a single overwrite, not an insert.
        _columnRepo.Verify(r => r.SetBusinessTermAsync(workspaceId, columnId, termId), Times.Once);
    }

    [Fact]
    public async Task MapToColumnAsync_ColumnInDifferentWorkspace_ThrowsColumnNotFoundException()
    {
        var workspaceId = Guid.NewGuid();
        var termId = Guid.NewGuid();
        var columnId = Guid.NewGuid();

        _termRepo.Setup(r => r.GetByIdAsync(termId)).ReturnsAsync(new BusinessTerm { Id = termId, WorkspaceId = workspaceId });
        _columnRepo.Setup(r => r.GetByIdAsync(workspaceId, columnId)).ReturnsAsync((Column?)null);

        await Assert.ThrowsAsync<ColumnNotFoundException>(() =>
            CreateService().MapToColumnAsync(workspaceId, columnId, termId));

        _columnRepo.Verify(r => r.SetBusinessTermAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>()), Times.Never);
    }

    [Fact]
    public async Task MapToColumnAsync_TermNotFound_ThrowsBusinessTermNotFoundException()
    {
        var workspaceId = Guid.NewGuid();
        var termId = Guid.NewGuid();
        _termRepo.Setup(r => r.GetByIdAsync(termId)).ReturnsAsync((BusinessTerm?)null);

        await Assert.ThrowsAsync<BusinessTermNotFoundException>(() =>
            CreateService().MapToColumnAsync(workspaceId, Guid.NewGuid(), termId));
    }

    [Fact]
    public async Task MapToColumnAsync_TermBelongsToDifferentWorkspace_ThrowsBusinessTermNotFoundException()
    {
        var workspaceId = Guid.NewGuid();
        var termId = Guid.NewGuid();
        var term = new BusinessTerm { Id = termId, WorkspaceId = Guid.NewGuid() }; // different workspace

        _termRepo.Setup(r => r.GetByIdAsync(termId)).ReturnsAsync(term);

        await Assert.ThrowsAsync<BusinessTermNotFoundException>(() =>
            CreateService().MapToColumnAsync(workspaceId, Guid.NewGuid(), termId));
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

        await CreateService().MapToColumnAsync(workspaceId, columnId, termId);

        _projectionService.Verify(p => p.SyncColumnTermAsync(workspaceId, columnId, "Revenue"), Times.Once);
        _projectionService.Verify(p => p.RefreshAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task MapToColumnAsync_DoesNotTouchProjectionOnValidationFailure()
    {
        var workspaceId = Guid.NewGuid();
        var termId = Guid.NewGuid();
        _termRepo.Setup(r => r.GetByIdAsync(termId)).ReturnsAsync((BusinessTerm?)null);

        await Assert.ThrowsAsync<BusinessTermNotFoundException>(() =>
            CreateService().MapToColumnAsync(workspaceId, Guid.NewGuid(), termId));

        _projectionService.Verify(p => p.RefreshAsync(It.IsAny<Guid>()), Times.Never);
        _projectionService.Verify(p => p.SyncColumnTermAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string?>()), Times.Never);
    }
}
