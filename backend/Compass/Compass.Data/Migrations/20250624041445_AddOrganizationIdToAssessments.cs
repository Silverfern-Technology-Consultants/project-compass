using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Compass.Data.Migrations;

/// <summary>
/// Add missing OrganizationId column to Assessments table
/// </summary>
public partial class AddOrganizationIdToAssessments : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Add OrganizationId column to Assessments table
        migrationBuilder.AddColumn<Guid>(
            name: "OrganizationId",
            table: "Assessments",
            type: "uniqueidentifier",
            nullable: true);

        // Update existing assessments to use the customer's organization
        migrationBuilder.Sql(@"
            UPDATE a
            SET a.OrganizationId = c.OrganizationId
            FROM Assessments a
            INNER JOIN Customers c ON a.CustomerId = c.CustomerId
            WHERE c.OrganizationId IS NOT NULL;
        ");

        // Add foreign key constraint
        migrationBuilder.AddForeignKey(
            name: "FK_Assessments_Organizations_OrganizationId",
            table: "Assessments",
            column: "OrganizationId",
            principalTable: "Organizations",
            principalColumn: "OrganizationId",
            onDelete: ReferentialAction.Restrict);

        // Add index for performance
        migrationBuilder.CreateIndex(
            name: "IX_Assessments_OrganizationId",
            table: "Assessments",
            column: "OrganizationId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Remove foreign key constraint
        migrationBuilder.DropForeignKey(
            name: "FK_Assessments_Organizations_OrganizationId",
            table: "Assessments");

        // Remove index
        migrationBuilder.DropIndex(
            name: "IX_Assessments_OrganizationId",
            table: "Assessments");

        // Remove column
        migrationBuilder.DropColumn(
            name: "OrganizationId",
            table: "Assessments");
    }
}