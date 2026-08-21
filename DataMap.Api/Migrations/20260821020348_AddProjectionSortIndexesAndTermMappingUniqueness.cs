using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataMap.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectionSortIndexesAndTermMappingUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TermColumnMappings_ColumnId",
                schema: "app",
                table: "TermColumnMappings");

            migrationBuilder.DropIndex(
                name: "IX_ColumnCatalogEditor_WorkspaceId",
                schema: "app",
                table: "ColumnCatalogEditor");

            // Existing databases may already hold several mappings per column — that is the
            // defect this unique index closes, and the unique index cannot be created while
            // the duplicates are present. Keep one mapping per column, drop the rest.
            migrationBuilder.Sql(@"
                DELETE FROM app.""TermColumnMappings"" m
                USING (
                    SELECT ""Id"", ROW_NUMBER() OVER (PARTITION BY ""ColumnId"" ORDER BY ""Id"") AS rn
                    FROM app.""TermColumnMappings""
                ) ranked
                WHERE m.""Id"" = ranked.""Id"" AND ranked.rn > 1;");

            migrationBuilder.CreateIndex(
                name: "IX_TermColumnMappings_ColumnId",
                schema: "app",
                table: "TermColumnMappings",
                column: "ColumnId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ColumnCatalogEditor_WorkspaceId_ColumnName_ColumnId",
                schema: "app",
                table: "ColumnCatalogEditor",
                columns: new[] { "WorkspaceId", "ColumnName", "ColumnId" });

            migrationBuilder.CreateIndex(
                name: "IX_ColumnCatalogEditor_WorkspaceId_DataType_ColumnId",
                schema: "app",
                table: "ColumnCatalogEditor",
                columns: new[] { "WorkspaceId", "DataType", "ColumnId" });

            migrationBuilder.CreateIndex(
                name: "IX_ColumnCatalogEditor_WorkspaceId_Owner_ColumnId",
                schema: "app",
                table: "ColumnCatalogEditor",
                columns: new[] { "WorkspaceId", "Owner", "ColumnId" });

            migrationBuilder.CreateIndex(
                name: "IX_ColumnCatalogEditor_WorkspaceId_TableName_ColumnId",
                schema: "app",
                table: "ColumnCatalogEditor",
                columns: new[] { "WorkspaceId", "TableName", "ColumnId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TermColumnMappings_ColumnId",
                schema: "app",
                table: "TermColumnMappings");

            migrationBuilder.DropIndex(
                name: "IX_ColumnCatalogEditor_WorkspaceId_ColumnName_ColumnId",
                schema: "app",
                table: "ColumnCatalogEditor");

            migrationBuilder.DropIndex(
                name: "IX_ColumnCatalogEditor_WorkspaceId_DataType_ColumnId",
                schema: "app",
                table: "ColumnCatalogEditor");

            migrationBuilder.DropIndex(
                name: "IX_ColumnCatalogEditor_WorkspaceId_Owner_ColumnId",
                schema: "app",
                table: "ColumnCatalogEditor");

            migrationBuilder.DropIndex(
                name: "IX_ColumnCatalogEditor_WorkspaceId_TableName_ColumnId",
                schema: "app",
                table: "ColumnCatalogEditor");

            migrationBuilder.CreateIndex(
                name: "IX_TermColumnMappings_ColumnId",
                schema: "app",
                table: "TermColumnMappings",
                column: "ColumnId");

            migrationBuilder.CreateIndex(
                name: "IX_ColumnCatalogEditor_WorkspaceId",
                schema: "app",
                table: "ColumnCatalogEditor",
                column: "WorkspaceId");
        }
    }
}
