using DataMap.Api.Data;
using DataMap.Api.Repositories;
using DataMap.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace DataMap.Api.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataMapServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.WithOrigins("http://localhost:5173")
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(config.GetConnectionString("DefaultConnection")));

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
        services.AddScoped<IInviteRepository, InviteRepository>();
        services.AddScoped<IParticipantRepository, ParticipantRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<ISchemaRepository, SchemaRepository>();
        services.AddScoped<ITableRepository, TableRepository>();
        services.AddScoped<IColumnRepository, ColumnRepository>();
        services.AddScoped<IBusinessTermRepository, BusinessTermRepository>();
        services.AddScoped<IProjectionRepository, ProjectionRepository>();
        services.AddScoped<IMetadataChangeRepository, MetadataChangeRepository>();

        services.AddScoped<IInviteService, InviteService>();
        services.AddScoped<IDevAccessService, DevAccessService>();
        services.AddScoped<IWorkspaceCopyService, WorkspaceCopyService>();
        services.AddScoped<IMetadataService, MetadataService>();
        services.AddScoped<IMetadataImportService, MetadataImportService>();
        services.AddScoped<IBusinessTermService, BusinessTermService>();
        services.AddScoped<IProjectionService, ProjectionService>();

        services.AddScoped<DataMap.Api.Seed.DemoDataSeeder>();

        return services;
    }
}
