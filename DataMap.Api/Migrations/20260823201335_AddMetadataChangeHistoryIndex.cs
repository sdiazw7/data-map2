using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataMap.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMetadataChangeHistoryIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MetadataChanges_EntityId",
                schema: "app",
                table: "MetadataChanges");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataChanges_EntityId_EditedAt",
                schema: "app",
                table: "MetadataChanges",
                columns: new[] { "EntityId", "EditedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MetadataChanges_EntityId_EditedAt",
                schema: "app",
                table: "MetadataChanges");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataChanges_EntityId",
                schema: "app",
                table: "MetadataChanges",
                column: "EntityId");
        }
    }
}
