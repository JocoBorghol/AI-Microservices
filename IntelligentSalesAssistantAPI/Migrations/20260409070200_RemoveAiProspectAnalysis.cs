using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelligentSalesAssistantAPI.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAiProspectAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompanyWebsites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrgNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CompanyName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Tone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    TargetAudience = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    TopServicesJson = table.Column<string>(type: "TEXT", nullable: true),
                    KeywordsJson = table.Column<string>(type: "TEXT", nullable: true),
                    GeneratedContentJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyWebsites", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyWebsites_Category",
                table: "CompanyWebsites",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyWebsites_CreatedAt",
                table: "CompanyWebsites",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyWebsites_OrgNumber",
                table: "CompanyWebsites",
                column: "OrgNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyWebsites");
        }
    }
}
