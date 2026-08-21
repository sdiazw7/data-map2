using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataMap.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexesAndConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BusinessTerms_WorkspaceId",
                schema: "app",
                table: "BusinessTerms");

            migrationBuilder.DropIndex(
                name: "IX_Columns_WorkspaceId",
                schema: "app",
                table: "Columns");

            migrationBuilder.DropIndex(
                name: "IX_Schemas_WorkspaceId",
                schema: "app",
                table: "Schemas");

            migrationBuilder.DropIndex(
                name: "IX_Tables_WorkspaceId",
                schema: "app",
                table: "Tables");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessTerms_WorkspaceId_Name",
                schema: "app",
                table: "BusinessTerms",
                columns: new[] { "WorkspaceId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ColumnCatalogEditor_WorkspaceId",
                schema: "app",
                table: "ColumnCatalogEditor",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Columns_WorkspaceId_TableId_Name",
                schema: "app",
                table: "Columns",
                columns: new[] { "WorkspaceId", "TableId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invites_Token",
                schema: "app",
                table: "Invites",
                column: "Token");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataChanges_EntityId",
                schema: "app",
                table: "MetadataChanges",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Schemas_WorkspaceId_Name",
                schema: "app",
                table: "Schemas",
                columns: new[] { "WorkspaceId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tables_WorkspaceId_SchemaId_Name",
                schema: "app",
                table: "Tables",
                columns: new[] { "WorkspaceId", "SchemaId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BusinessTerms_WorkspaceId_Name",
                schema: "app",
                table: "BusinessTerms");

            migrationBuilder.DropIndex(
                name: "IX_ColumnCatalogEditor_WorkspaceId",
                schema: "app",
                table: "ColumnCatalogEditor");

            migrationBuilder.DropIndex(
                name: "IX_Columns_WorkspaceId_TableId_Name",
                schema: "app",
                table: "Columns");

            migrationBuilder.DropIndex(
                name: "IX_Invites_Token",
                schema: "app",
                table: "Invites");

            migrationBuilder.DropIndex(
                name: "IX_MetadataChanges_EntityId",
                schema: "app",
                table: "MetadataChanges");

            migrationBuilder.DropIndex(
                name: "IX_Schemas_WorkspaceId_Name",
                schema: "app",
                table: "Schemas");

            migrationBuilder.DropIndex(
                name: "IX_Tables_WorkspaceId_SchemaId_Name",
                schema: "app",
                table: "Tables");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessTerms_WorkspaceId",
                schema: "app",
                table: "BusinessTerms",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Columns_WorkspaceId",
                schema: "app",
                table: "Columns",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Schemas_WorkspaceId",
                schema: "app",
                table: "Schemas",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Tables_WorkspaceId",
                schema: "app",
                table: "Tables",
                column: "WorkspaceId");
        }
    }
}
