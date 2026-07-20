using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyGraphRagV5.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "app");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "RagProjects",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    SourceFolder = table.Column<string>(type: "text", nullable: false),
                    GraphName = table.Column<string>(type: "text", nullable: false),
                    VectorCollection = table.Column<string>(type: "text", nullable: false),
                    FilePattern = table.Column<string>(type: "text", nullable: false),
                    EmbeddingDimension = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RagProjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChatSessions",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatSessions_RagProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "app",
                        principalTable: "RagProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IndexingRuns",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CurrentWorkflow = table.Column<string>(type: "text", nullable: true),
                    ProgressPercent = table.Column<double>(type: "double precision", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true),
                    DocumentCount = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndexingRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IndexingRuns_RagProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "app",
                        principalTable: "RagProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatMessages",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    SourcesJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatMessages_ChatSessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "app",
                        principalTable: "ChatSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_SessionId",
                schema: "app",
                table: "ChatMessages",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_ProjectId",
                schema: "app",
                table: "ChatSessions",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_IndexingRuns_ProjectId",
                schema: "app",
                table: "IndexingRuns",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_RagProjects_GraphName",
                schema: "app",
                table: "RagProjects",
                column: "GraphName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RagProjects_Name",
                schema: "app",
                table: "RagProjects",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatMessages",
                schema: "app");

            migrationBuilder.DropTable(
                name: "IndexingRuns",
                schema: "app");

            migrationBuilder.DropTable(
                name: "ChatSessions",
                schema: "app");

            migrationBuilder.DropTable(
                name: "RagProjects",
                schema: "app");
        }
    }
}
