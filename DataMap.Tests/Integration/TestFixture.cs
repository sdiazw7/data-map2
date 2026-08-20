using DataMap.Api.Data;
using DataMap.Api.Models;
using DataMap.Api.Repositories;
using DataMap.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace DataMap.Tests.Integration;

public class TestFixture : WebApplicationFactory<Program>
{
    // Stable test identity used for authenticated requests
    public static readonly Guid TestWorkspaceId = Guid.NewGuid();
    public static readonly Guid TestParticipantId = Guid.NewGuid();
    public static readonly Guid TestSessionId = Guid.Parse("10000000-0000-0000-0000-000000000001");

    private static readonly ParticipantSession TestSession = new()
    {
        Id = TestSessionId,
        ParticipantId = TestParticipantId,
        WorkspaceId = TestWorkspaceId,
        LastSeenAt = DateTime.UtcNow
    };

    // Publicly accessible mocks so tests can configure and verify them
    public readonly Mock<ISessionRepository> SessionRepo = new();
    public readonly Mock<IProjectionRepository> ProjectionRepo = new();
    public readonly Mock<IInviteService> InviteService = new();
    public readonly Mock<IMetadataService> MetadataService = new();
    public readonly Mock<IBusinessTermService> BusinessTermService = new();

    public TestFixture()
    {
        // Session repo always authenticates our test session ID
        SessionRepo.Setup(r => r.GetByIdAsync(TestSessionId)).ReturnsAsync(TestSession);
        SessionRepo.Setup(r => r.UpdateLastSeenAtAsync(TestSessionId, It.IsAny<DateTime>())).Returns(Task.CompletedTask);

        // Projection repo is a no-op (raw SQL incompatible with in-memory DB)
        ProjectionRepo.Setup(r => r.RefreshAsync(It.IsAny<Guid>())).Returns(Task.CompletedTask);
        ProjectionRepo.Setup(r => r.QueryAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync([]);
        ProjectionRepo.Setup(r => r.GetCoverageCountsAsync(It.IsAny<Guid>())).ReturnsAsync((0, 0));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Replace PostgreSQL with in-memory EF. We bypass the DI options pipeline entirely
            // to avoid EF's "two providers registered" error (Npgsql services are already in the
            // container; adding InMemory on top of them via AddDbContext produces a conflict).
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.AddScoped<AppDbContext>(_ =>
            {
                var opts = new DbContextOptionsBuilder<AppDbContext>()
                    .UseInMemoryDatabase("TestDb_" + Guid.NewGuid().ToString("N"))
                    .Options;
                return new AppDbContext(opts);
            });

            // Replace raw-SQL repositories with mocks
            ReplaceScoped<IProjectionRepository>(services, _ => ProjectionRepo.Object);
            ReplaceScoped<ISessionRepository>(services, _ => SessionRepo.Object);

            // Replace application services with mocks
            ReplaceScoped<IInviteService>(services, _ => InviteService.Object);
            ReplaceScoped<IMetadataService>(services, _ => MetadataService.Object);
            ReplaceScoped<IBusinessTermService>(services, _ => BusinessTermService.Object);
        });
    }

    private static void ReplaceScoped<TService>(IServiceCollection services, Func<IServiceProvider, TService> factory)
        where TService : class
    {
        var descriptor = services.Single(d => d.ServiceType == typeof(TService));
        services.Remove(descriptor);
        services.AddScoped(factory);
    }

    /// <summary>Creates an HttpClient with the test session cookie pre-attached.</summary>
    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("Cookie", $"participant_session={TestSessionId}");
        return client;
    }
}
