using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataMap.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplateWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTemplate",
                schema: "app",
                table: "Workspaces",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceTemplateId",
                schema: "app",
                table: "Workspaces",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TemplateWorkspaceId",
                schema: "app",
                table: "Invites",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsTemplate",
                schema: "app",
                table: "Workspaces");

            migrationBuilder.DropColumn(
                name: "SourceTemplateId",
                schema: "app",
                table: "Workspaces");

            migrationBuilder.DropColumn(
                name: "TemplateWorkspaceId",
                schema: "app",
                table: "Invites");
        }
    }
}
