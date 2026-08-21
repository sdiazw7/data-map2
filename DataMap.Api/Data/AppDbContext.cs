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

        // The grid saves optimistically, so two participants can hold the same row and submit
        // edits at once. As a concurrency token, Version lands in the UPDATE's WHERE clause with
        // the value that was read, so the second write matches no row and EF raises
        // DbUpdateConcurrencyException instead of silently overwriting the first edit.
        // No schema change: EF only uses this to shape the generated SQL.
        modelBuilder.Entity<Column>()
            .Property(c => c.Version)
            .IsConcurrencyToken();

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

        // A column carries at most one business term. The projection has a single
        // BusinessTerm field, so a second mapping would fan the rebuild join out into
        // two rows sharing one ColumnId — which is the projection's primary key.
        modelBuilder.Entity<TermColumnMapping>()
            .HasIndex(m => m.ColumnId)
            .IsUnique();

        // Performance indexes not auto-created by EF
        modelBuilder.Entity<Invite>()
            .HasIndex(i => i.Token);

        modelBuilder.Entity<MetadataChange>()
            .HasIndex(m => m.EntityId);

        // One index per grid sort key. Each leads with WorkspaceId (every query filters
        // on it) and trails with ColumnId (the pagination tie-breaker), so Postgres can
        // serve filter + ORDER BY + LIMIT from a single index scan rather than sorting
        // the workspace's entire row set on every page request. A standalone WorkspaceId
        // index would be a redundant prefix of these.
        modelBuilder.Entity<ColumnCatalogEditor>()
            .HasIndex(c => new { c.WorkspaceId, c.ColumnName, c.ColumnId });

        modelBuilder.Entity<ColumnCatalogEditor>()
            .HasIndex(c => new { c.WorkspaceId, c.TableName, c.ColumnId });

        modelBuilder.Entity<ColumnCatalogEditor>()
            .HasIndex(c => new { c.WorkspaceId, c.DataType, c.ColumnId });

        modelBuilder.Entity<ColumnCatalogEditor>()
            .HasIndex(c => new { c.WorkspaceId, c.Owner, c.ColumnId });
    }
}
