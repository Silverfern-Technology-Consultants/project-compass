using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Compass.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCostManagementPermissionTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvailablePermissions",
                table: "AzureEnvironments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CostManagementLastChecked",
                table: "AzureEnvironments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CostManagementLastError",
                table: "AzureEnvironments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CostManagementSetupStatus",
                table: "AzureEnvironments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasCostManagementAccess",
                table: "AzureEnvironments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MissingPermissions",
                table: "AzureEnvironments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AzureEnvironments_ClientId_HasCostManagementAccess",
                table: "AzureEnvironments",
                columns: new[] { "ClientId", "HasCostManagementAccess" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AzureEnvironments_ClientId_HasCostManagementAccess",
                table: "AzureEnvironments");

            migrationBuilder.DropColumn(
                name: "AvailablePermissions",
                table: "AzureEnvironments");

            migrationBuilder.DropColumn(
                name: "CostManagementLastChecked",
                table: "AzureEnvironments");

            migrationBuilder.DropColumn(
                name: "CostManagementLastError",
                table: "AzureEnvironments");

            migrationBuilder.DropColumn(
                name: "CostManagementSetupStatus",
                table: "AzureEnvironments");

            migrationBuilder.DropColumn(
                name: "HasCostManagementAccess",
                table: "AzureEnvironments");

            migrationBuilder.DropColumn(
                name: "MissingPermissions",
                table: "AzureEnvironments");
        }
    }
}
