using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelligentSalesAssistantAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatedByToWebsite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "CompanyWebsites",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "CompanyWebsites");
        }
    }
}
