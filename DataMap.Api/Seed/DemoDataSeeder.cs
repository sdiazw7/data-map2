using DataMap.Api.Data;
using DataMap.Api.Models;
using DataMap.Api.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DataMap.Api.Seed;

public class DemoDataSeeder(AppDbContext db, IProjectionRepository projectionRepo)
{
    public async Task SeedAsync()
    {
        var workspaceName = "Acme Commerce Analytics";

        if (await db.Workspaces.AnyAsync(w => w.Name == workspaceName))
            return;

        var now = DateTime.UtcNow;

        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = workspaceName,
            CreatedAt = now
        };
        db.Workspaces.Add(workspace);

        var invite = new Invite
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            Token = "demo",
            CreatedAt = now,
            ExpiresAt = now.AddYears(1),
            MaxUses = 100,
            UsedCount = 0
        };
        db.Invites.Add(invite);

        // Schemas
        var salesSchema = new DataMap.Api.Models.Schema { Id = Guid.NewGuid(), WorkspaceId = workspace.Id, Name = "sales" };
        var marketingSchema = new DataMap.Api.Models.Schema { Id = Guid.NewGuid(), WorkspaceId = workspace.Id, Name = "marketing" };
        var productSchema = new DataMap.Api.Models.Schema { Id = Guid.NewGuid(), WorkspaceId = workspace.Id, Name = "product" };
        db.Schemas.AddRange(salesSchema, marketingSchema, productSchema);

        // Tables
        var ordersTable = new Table { Id = Guid.NewGuid(), WorkspaceId = workspace.Id, SchemaId = salesSchema.Id, Name = "orders", CreatedAt = now, UpdatedAt = now };
        var orderItemsTable = new Table { Id = Guid.NewGuid(), WorkspaceId = workspace.Id, SchemaId = salesSchema.Id, Name = "order_items", CreatedAt = now, UpdatedAt = now };
        var customersTable = new Table { Id = Guid.NewGuid(), WorkspaceId = workspace.Id, SchemaId = salesSchema.Id, Name = "customers", CreatedAt = now, UpdatedAt = now };
        var campaignsTable = new Table { Id = Guid.NewGuid(), WorkspaceId = workspace.Id, SchemaId = marketingSchema.Id, Name = "campaigns", CreatedAt = now, UpdatedAt = now };
        var leadsTable = new Table { Id = Guid.NewGuid(), WorkspaceId = workspace.Id, SchemaId = marketingSchema.Id, Name = "leads", CreatedAt = now, UpdatedAt = now };
        var productsTable = new Table { Id = Guid.NewGuid(), WorkspaceId = workspace.Id, SchemaId = productSchema.Id, Name = "products", CreatedAt = now, UpdatedAt = now };
        var categoriesTable = new Table { Id = Guid.NewGuid(), WorkspaceId = workspace.Id, SchemaId = productSchema.Id, Name = "categories", CreatedAt = now, UpdatedAt = now };
        var inventoryTable = new Table { Id = Guid.NewGuid(), WorkspaceId = workspace.Id, SchemaId = productSchema.Id, Name = "inventory", CreatedAt = now, UpdatedAt = now };
        db.Tables.AddRange(ordersTable, orderItemsTable, customersTable, campaignsTable, leadsTable, productsTable, categoriesTable, inventoryTable);

        // Columns — orders
        AddColumns(db, workspace.Id, ordersTable.Id, now,
            ("id", "uuid"),
            ("customer_id", "uuid"),
            ("status", "varchar"),
            ("total_amount", "numeric"),
            ("created_at", "timestamp"));

        // Columns — order_items
        AddColumns(db, workspace.Id, orderItemsTable.Id, now,
            ("id", "uuid"),
            ("order_id", "uuid"),
            ("product_id", "uuid"),
            ("quantity", "int"),
            ("unit_price", "numeric"));

        // Columns — customers
        AddColumns(db, workspace.Id, customersTable.Id, now,
            ("id", "uuid"),
            ("email", "varchar"),
            ("first_name", "varchar"),
            ("last_name", "varchar"),
            ("created_at", "timestamp"));

        // Columns — campaigns
        AddColumns(db, workspace.Id, campaignsTable.Id, now,
            ("id", "uuid"),
            ("name", "varchar"),
            ("channel", "varchar"),
            ("budget", "numeric"),
            ("start_date", "date"),
            ("end_date", "date"));

        // Columns — leads
        AddColumns(db, workspace.Id, leadsTable.Id, now,
            ("id", "uuid"),
            ("email", "varchar"),
            ("source", "varchar"),
            ("status", "varchar"),
            ("created_at", "timestamp"));

        // Columns — products
        AddColumns(db, workspace.Id, productsTable.Id, now,
            ("id", "uuid"),
            ("name", "varchar"),
            ("sku", "varchar"),
            ("price", "numeric"),
            ("category_id", "uuid"));

        // Columns — categories
        AddColumns(db, workspace.Id, categoriesTable.Id, now,
            ("id", "uuid"),
            ("name", "varchar"),
            ("parent_id", "uuid"));

        // Columns — inventory
        AddColumns(db, workspace.Id, inventoryTable.Id, now,
            ("id", "uuid"),
            ("product_id", "uuid"),
            ("warehouse", "varchar"),
            ("quantity", "int"),
            ("updated_at", "timestamp"));

        await db.SaveChangesAsync();

        await projectionRepo.RefreshAsync(workspace.Id);
    }

    private static void AddColumns(AppDbContext db, Guid workspaceId, Guid tableId, DateTime now, params (string Name, string DataType)[] columns)
    {
        foreach (var (name, dataType) in columns)
        {
            db.Columns.Add(new Column
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                TableId = tableId,
                Name = name,
                DataType = dataType,
                Version = 1,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
    }
}
