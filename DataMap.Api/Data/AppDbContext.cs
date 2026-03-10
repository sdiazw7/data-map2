using DataMap.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DataMap.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<Invite> Invites => Set<Invite>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<ParticipantSession> ParticipantSessions => Set<ParticipantSession>();
    public DbSet<Schema> Schemas => Set<Schema>();
    public DbSet<Table> Tables => Set<Table>();
    public DbSet<Column> Columns => Set<Column>();
    public DbSet<Relationship> Relationships => Set<Relationship>();
    public DbSet<BusinessTerm> BusinessTerms => Set<BusinessTerm>();
    public DbSet<TermColumnMapping> TermColumnMappings => Set<TermColumnMapping>();
    public DbSet<MetadataChange> MetadataChanges => Set<MetadataChange>();
    public DbSet<ColumnCatalogEditor> ColumnCatalogEditor => Set<ColumnCatalogEditor>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("app");

        modelBuilder.Entity<Participant>()
            .HasIndex(p => new { p.WorkspaceId, p.Email })
            .IsUnique();

        modelBuilder.Entity<ColumnCatalogEditor>()
            .HasKey(c => c.ColumnId);

        // Unique constraints
        modelBuilder.Entity<Schema>()
            .HasIndex(s => new { s.WorkspaceId, s.Name })
            .IsUnique();

        modelBuilder.Entity<Table>()
            .HasIndex(t => new { t.WorkspaceId, t.SchemaId, t.Name })
            .IsUnique();

        modelBuilder.Entity<Column>()
            .HasIndex(c => new { c.WorkspaceId, c.TableId, c.Name })
            .IsUnique();

        modelBuilder.Entity<BusinessTerm>()
            .HasIndex(b => new { b.WorkspaceId, b.Name })
            .IsUnique();

        // Performance indexes not auto-created by EF
        modelBuilder.Entity<Invite>()
            .HasIndex(i => i.Token);

        modelBuilder.Entity<MetadataChange>()
            .HasIndex(m => m.EntityId);

        modelBuilder.Entity<ColumnCatalogEditor>()
            .HasIndex(c => c.WorkspaceId);
    }
}
