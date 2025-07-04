using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Compass.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEnhancedClientPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomTags",
                table: "ClientPreferences",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnvironmentIndicatorLevel",
                table: "ClientPreferences",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnvironmentSize",
                table: "ClientPreferences",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NamingStyle",
                table: "ClientPreferences",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NoSpecificRequirements",
                table: "ClientPreferences",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OrganizationMethod",
                table: "ClientPreferences",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelectedCompliances",
                table: "ClientPreferences",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelectedTags",
                table: "ClientPreferences",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaggingApproach",
                table: "ClientPreferences",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomTags",
                table: "ClientPreferences");

            migrationBuilder.DropColumn(
                name: "EnvironmentIndicatorLevel",
                table: "ClientPreferences");

            migrationBuilder.DropColumn(
                name: "EnvironmentSize",
                table: "ClientPreferences");

            migrationBuilder.DropColumn(
                name: "NamingStyle",
                table: "ClientPreferences");

            migrationBuilder.DropColumn(
                name: "NoSpecificRequirements",
                table: "ClientPreferences");

            migrationBuilder.DropColumn(
                name: "OrganizationMethod",
                table: "ClientPreferences");

            migrationBuilder.DropColumn(
                name: "SelectedCompliances",
                table: "ClientPreferences");

            migrationBuilder.DropColumn(
                name: "SelectedTags",
                table: "ClientPreferences");

            migrationBuilder.DropColumn(
                name: "TaggingApproach",
                table: "ClientPreferences");
        }
    }
}
