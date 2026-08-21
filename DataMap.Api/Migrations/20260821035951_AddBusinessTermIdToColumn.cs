using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataMap.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessTermIdToColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BusinessTermId",
                schema: "app",
                table: "Columns",
                type: "uuid",
                nullable: true);

            // TermColumnMappings.ColumnId is uniquely indexed, so each column has at most one
            // row to carry over before the table is dropped.
            migrationBuilder.Sql(@"
                UPDATE app.""Columns"" c
                SET ""BusinessTermId"" = tcm.""TermId""
                FROM app.""TermColumnMappings"" tcm
                WHERE tcm.""ColumnId"" = c.""Id"";");

            migrationBuilder.DropTable(
                name: "TermColumnMappings",
                schema: "app");

            migrationBuilder.CreateIndex(
                name: "IX_Columns_BusinessTermId",
                schema: "app",
                table: "Columns",
                column: "BusinessTermId");

            migrationBuilder.AddForeignKey(
                name: "FK_Columns_BusinessTerms_BusinessTermId",
                schema: "app",
                table: "Columns",
                column: "BusinessTermId",
                principalSchema: "app",
                principalTable: "BusinessTerms",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TermColumnMappings",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ColumnId = table.Column<Guid>(type: "uuid", nullable: false),
                    TermId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TermColumnMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TermColumnMappings_BusinessTerms_TermId",
                        column: x => x.TermId,
                        principalSchema: "app",
                        principalTable: "BusinessTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TermColumnMappings_Columns_ColumnId",
                        column: x => x.ColumnId,
                        principalSchema: "app",
                        principalTable: "Columns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(@"
                INSERT INTO app.""TermColumnMappings"" (""Id"", ""ColumnId"", ""TermId"")
                SELECT gen_random_uuid(), c.""Id"", c.""BusinessTermId""
                FROM app.""Columns"" c
                WHERE c.""BusinessTermId"" IS NOT NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_TermColumnMappings_ColumnId",
                schema: "app",
                table: "TermColumnMappings",
                column: "ColumnId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TermColumnMappings_TermId",
                schema: "app",
                table: "TermColumnMappings",
                column: "TermId");

            migrationBuilder.DropForeignKey(
                name: "FK_Columns_BusinessTerms_BusinessTermId",
                schema: "app",
                table: "Columns");

            migrationBuilder.DropIndex(
                name: "IX_Columns_BusinessTermId",
                schema: "app",
                table: "Columns");

            migrationBuilder.DropColumn(
                name: "BusinessTermId",
                schema: "app",
                table: "Columns");
        }
    }
}
