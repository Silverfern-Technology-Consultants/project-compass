using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Compass.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationIdsToEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ✅ SAFE: Only add OrganizationId to Subscriptions (Assessments already has it)
            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Subscriptions",
                type: "uniqueidentifier",
                nullable: true);

            // ✅ ADD: Index for Subscriptions (Assessments index might already exist)
            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_OrganizationId",
                table: "Subscriptions",
                column: "OrganizationId");

            // ✅ ADD: Foreign key constraints
            migrationBuilder.AddForeignKey(
                name: "FK_Subscriptions_Organizations_OrganizationId",
                table: "Subscriptions",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "OrganizationId",
                onDelete: ReferentialAction.Restrict);

            // ✅ TRY: Add Assessments foreign key (if it doesn't exist)
            try
            {
                migrationBuilder.AddForeignKey(
                    name: "FK_Assessments_Organizations_OrganizationId",
                    table: "Assessments",
                    column: "OrganizationId",
                    principalTable: "Organizations",
                    principalColumn: "OrganizationId",
                    onDelete: ReferentialAction.Restrict);
            }
            catch
            {
                // Foreign key might already exist, ignore error
            }

            // ✅ ADD: Index for Assessments if it doesn't exist
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Assessments_OrganizationId')
                BEGIN
                    CREATE INDEX IX_Assessments_OrganizationId ON Assessments (OrganizationId)
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop foreign keys
            migrationBuilder.DropForeignKey(
                name: "FK_Subscriptions_Organizations_OrganizationId",
                table: "Subscriptions");

            // Drop indexes
            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_OrganizationId",
                table: "Subscriptions");

            // Drop columns
            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Subscriptions");
        }
    }
}