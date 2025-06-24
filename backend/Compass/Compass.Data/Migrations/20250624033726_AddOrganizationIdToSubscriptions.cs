using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Compass.Data.Migrations;

/// <summary>
/// Add missing OrganizationId column to Subscriptions table
/// </summary>
public partial class AddOrganizationIdToSubscriptions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Add OrganizationId column to Subscriptions table
        migrationBuilder.AddColumn<Guid>(
            name: "OrganizationId",
            table: "Subscriptions",
            type: "uniqueidentifier",
            nullable: true);

        // Update existing subscriptions to use the customer's organization
        migrationBuilder.Sql(@"
            UPDATE s
            SET s.OrganizationId = c.OrganizationId
            FROM Subscriptions s
            INNER JOIN Customers c ON s.CustomerId = c.CustomerId
            WHERE c.OrganizationId IS NOT NULL;
        ");

        // Add foreign key constraint
        migrationBuilder.AddForeignKey(
            name: "FK_Subscriptions_Organizations_OrganizationId",
            table: "Subscriptions",
            column: "OrganizationId",
            principalTable: "Organizations",
            principalColumn: "OrganizationId",
            onDelete: ReferentialAction.Restrict);

        // Add index for performance
        migrationBuilder.CreateIndex(
            name: "IX_Subscriptions_OrganizationId",
            table: "Subscriptions",
            column: "OrganizationId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Remove foreign key constraint
        migrationBuilder.DropForeignKey(
            name: "FK_Subscriptions_Organizations_OrganizationId",
            table: "Subscriptions");

        // Remove index
        migrationBuilder.DropIndex(
            name: "IX_Subscriptions_OrganizationId",
            table: "Subscriptions");

        // Remove column
        migrationBuilder.DropColumn(
            name: "OrganizationId",
            table: "Subscriptions");
    }
}