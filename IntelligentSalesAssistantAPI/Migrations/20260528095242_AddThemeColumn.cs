using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelligentSalesAssistantAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddThemeColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Theme",
                table: "CompanyWebsites",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Theme",
                table: "CompanyWebsites");
        }
    }
}
