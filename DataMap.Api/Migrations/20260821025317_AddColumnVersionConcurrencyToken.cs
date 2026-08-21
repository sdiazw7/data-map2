using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataMap.Api.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Intentionally empty. Marking Column.Version as a concurrency token changes only the SQL
    /// EF generates — it adds the read Version to each UPDATE's WHERE clause — and needs no DDL.
    /// This migration exists to keep the model snapshot in step with that change.
    /// </summary>
    public partial class AddColumnVersionConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
