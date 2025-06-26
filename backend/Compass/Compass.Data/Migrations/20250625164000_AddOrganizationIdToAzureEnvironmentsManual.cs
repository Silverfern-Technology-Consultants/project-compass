using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Compass.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationIdToAzureEnvironmentsManual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add OrganizationId column to AzureEnvironments table
            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "AzureEnvironments",
                type: "uniqueidentifier",
                nullable: true);

            // Create index on OrganizationId
            migrationBuilder.CreateIndex(
                name: "IX_AzureEnvironments_OrganizationId",
                table: "AzureEnvironments",
                column: "OrganizationId");

            // Add foreign key constraint
            migrationBuilder.AddForeignKey(
                name: "FK_AzureEnvironments_Organizations_OrganizationId",
                table: "AzureEnvironments",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "OrganizationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove foreign key constraint
            migrationBuilder.DropForeignKey(
                name: "FK_AzureEnvironments_Organizations_OrganizationId",
                table: "AzureEnvironments");

            // Remove index
            migrationBuilder.DropIndex(
                name: "IX_AzureEnvironments_OrganizationId",
                table: "AzureEnvironments");

            // Remove OrganizationId column
            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "AzureEnvironments");
        }
    }
}