using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelligentSalesAssistantAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddContentDraftTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContentDrafts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WebsiteId = table.Column<int>(type: "INTEGER", nullable: true),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Instructions = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Purpose = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    TargetAudience = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Tone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Length = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    OriginalContentPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ModifiedContentPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CompanyName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentDrafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentDrafts_CompanyWebsites_WebsiteId",
                        column: x => x.WebsiteId,
                        principalTable: "CompanyWebsites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentDrafts_CreatedAt",
                table: "ContentDrafts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ContentDrafts_WebsiteId_CreatedAt",
                table: "ContentDrafts",
                columns: new[] { "WebsiteId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentDrafts");
        }
    }
}
