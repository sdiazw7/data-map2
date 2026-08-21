using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataMap.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "app");

            migrationBuilder.CreateTable(
                name: "ColumnCatalogEditor",
                schema: "app",
                columns: table => new
                {
                    ColumnId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SchemaName = table.Column<string>(type: "text", nullable: false),
                    TableName = table.Column<string>(type: "text", nullable: false),
                    ColumnName = table.Column<string>(type: "text", nullable: false),
                    DataType = table.Column<string>(type: "text", nullable: false),
                    ExampleValue = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    BusinessTerm = table.Column<string>(type: "text", nullable: true),
                    Owner = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ColumnCatalogEditor", x => x.ColumnId);
                });

            migrationBuilder.CreateTable(
                name: "Workspaces",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspaces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BusinessTerms",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Definition = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessTerms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessTerms_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalSchema: "app",
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Invites",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MaxUses = table.Column<int>(type: "integer", nullable: false),
                    UsedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invites_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalSchema: "app",
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Schemas",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Schemas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Schemas_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalSchema: "app",
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Participants",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    InviteId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Participants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Participants_Invites_InviteId",
                        column: x => x.InviteId,
                        principalSchema: "app",
                        principalTable: "Invites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Participants_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalSchema: "app",
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tables",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SchemaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tables_Schemas_SchemaId",
                        column: x => x.SchemaId,
                        principalSchema: "app",
                        principalTable: "Schemas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Tables_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalSchema: "app",
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MetadataChanges",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Field = table.Column<string>(type: "text", nullable: false),
                    OldValue = table.Column<string>(type: "text", nullable: true),
                    NewValue = table.Column<string>(type: "text", nullable: true),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EditedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetadataChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetadataChanges_Participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalSchema: "app",
                        principalTable: "Participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ParticipantSessions",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParticipantSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParticipantSessions_Participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalSchema: "app",
                        principalTable: "Participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ParticipantSessions_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalSchema: "app",
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Columns",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    TableId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    DataType = table.Column<string>(type: "text", nullable: false),
                    ExampleValue = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Owner = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Columns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Columns_Tables_TableId",
                        column: x => x.TableId,
                        principalSchema: "app",
                        principalTable: "Tables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Columns_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalSchema: "app",
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Relationships",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceColumnId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetColumnId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelationshipType = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Relationships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Relationships_Columns_SourceColumnId",
                        column: x => x.SourceColumnId,
                        principalSchema: "app",
                        principalTable: "Columns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Relationships_Columns_TargetColumnId",
                        column: x => x.TargetColumnId,
                        principalSchema: "app",
                        principalTable: "Columns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Relationships_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalSchema: "app",
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TermColumnMappings",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TermId = table.Column<Guid>(type: "uuid", nullable: false),
                    ColumnId = table.Column<Guid>(type: "uuid", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_BusinessTerms_WorkspaceId",
                schema: "app",
                table: "BusinessTerms",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Columns_TableId",
                schema: "app",
                table: "Columns",
                column: "TableId");

            migrationBuilder.CreateIndex(
                name: "IX_Columns_WorkspaceId",
                schema: "app",
                table: "Columns",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Invites_WorkspaceId",
                schema: "app",
                table: "Invites",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataChanges_ParticipantId",
                schema: "app",
                table: "MetadataChanges",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_Participants_InviteId",
                schema: "app",
                table: "Participants",
                column: "InviteId");

            migrationBuilder.CreateIndex(
                name: "IX_Participants_WorkspaceId_Email",
                schema: "app",
                table: "Participants",
                columns: new[] { "WorkspaceId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantSessions_ParticipantId",
                schema: "app",
                table: "ParticipantSessions",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantSessions_WorkspaceId",
                schema: "app",
                table: "ParticipantSessions",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Relationships_SourceColumnId",
                schema: "app",
                table: "Relationships",
                column: "SourceColumnId");

            migrationBuilder.CreateIndex(
                name: "IX_Relationships_TargetColumnId",
                schema: "app",
                table: "Relationships",
                column: "TargetColumnId");

            migrationBuilder.CreateIndex(
                name: "IX_Relationships_WorkspaceId",
                schema: "app",
                table: "Relationships",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Schemas_WorkspaceId",
                schema: "app",
                table: "Schemas",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Tables_SchemaId",
                schema: "app",
                table: "Tables",
                column: "SchemaId");

            migrationBuilder.CreateIndex(
                name: "IX_Tables_WorkspaceId",
                schema: "app",
                table: "Tables",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_TermColumnMappings_ColumnId",
                schema: "app",
                table: "TermColumnMappings",
                column: "ColumnId");

            migrationBuilder.CreateIndex(
                name: "IX_TermColumnMappings_TermId",
                schema: "app",
                table: "TermColumnMappings",
                column: "TermId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ColumnCatalogEditor",
                schema: "app");

            migrationBuilder.DropTable(
                name: "MetadataChanges",
                schema: "app");

            migrationBuilder.DropTable(
                name: "ParticipantSessions",
                schema: "app");

            migrationBuilder.DropTable(
                name: "Relationships",
                schema: "app");

            migrationBuilder.DropTable(
                name: "TermColumnMappings",
                schema: "app");

            migrationBuilder.DropTable(
                name: "Participants",
                schema: "app");

            migrationBuilder.DropTable(
                name: "BusinessTerms",
                schema: "app");

            migrationBuilder.DropTable(
                name: "Columns",
                schema: "app");

            migrationBuilder.DropTable(
                name: "Invites",
                schema: "app");

            migrationBuilder.DropTable(
                name: "Tables",
                schema: "app");

            migrationBuilder.DropTable(
                name: "Schemas",
                schema: "app");

            migrationBuilder.DropTable(
                name: "Workspaces",
                schema: "app");
        }
    }
}
