using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Compass.Data.Migrations;

/// <summary>
/// Migration to add Organization support with proper data handling
/// </summary>
public partial class AddOrganizationSupportWithDataHandling : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Step 1: Create Organizations table
        migrationBuilder.CreateTable(
            name: "Organizations",
            columns: table => new
            {
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Active"),
                OrganizationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "MSP"),
                IsTrialOrganization = table.Column<bool>(type: "bit", nullable: false),
                TrialStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                TrialEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                Settings = table.Column<string>(type: "nvarchar(max)", nullable: true),
                TimeZone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Country = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Organizations", x => x.OrganizationId);
            });

        // Step 2: Add new columns to Customers table (nullable initially)
        migrationBuilder.AddColumn<Guid>(
            name: "OrganizationId",
            table: "Customers",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Role",
            table: "Customers",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: false,
            defaultValue: "Owner");

        // Step 3: Create organizations for existing customers and update customer records
        migrationBuilder.Sql(@"
            -- Create organizations for each existing customer
            INSERT INTO Organizations (OrganizationId, Name, Description, OwnerId, CreatedDate, Status, OrganizationType, IsTrialOrganization, TrialStartDate, TrialEndDate, TimeZone, Country)
            SELECT 
                NEWID() as OrganizationId,
                CompanyName as Name,
                'Organization for ' + CompanyName as Description,
                CustomerId as OwnerId,
                CreatedDate,
                'Active' as Status,
                'MSP' as OrganizationType,
                IsTrialAccount as IsTrialOrganization,
                TrialStartDate,
                TrialEndDate,
                TimeZone,
                Country
            FROM Customers;

            -- Update customers with their organization IDs
            UPDATE c
            SET c.OrganizationId = o.OrganizationId,
                c.Role = 'Owner'
            FROM Customers c
            INNER JOIN Organizations o ON c.CustomerId = o.OwnerId;
        ");

        // Step 4: Handle existing team invitations by mapping them to the correct organizations
        migrationBuilder.Sql(@"
            -- Update TeamInvitations to use the correct OrganizationId based on the inviter's organization
            UPDATE ti
            SET ti.OrganizationId = c.OrganizationId
            FROM TeamInvitations ti
            INNER JOIN Customers c ON ti.InvitedByCustomerId = c.CustomerId
            WHERE c.OrganizationId IS NOT NULL;

            -- Delete any orphaned invitations where the inviter doesn't have an organization
            DELETE FROM TeamInvitations 
            WHERE OrganizationId NOT IN (SELECT OrganizationId FROM Organizations);
        ");

        // Step 5: Add InvitationMessage column to TeamInvitations
        migrationBuilder.AddColumn<string>(
            name: "InvitationMessage",
            table: "TeamInvitations",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: true);

        // Step 6: Create indexes for Organizations
        migrationBuilder.CreateIndex(
            name: "IX_Organizations_Name",
            table: "Organizations",
            column: "Name");

        migrationBuilder.CreateIndex(
            name: "IX_Organizations_OwnerId",
            table: "Organizations",
            column: "OwnerId");

        migrationBuilder.CreateIndex(
            name: "IX_Organizations_Status_OrganizationType",
            table: "Organizations",
            columns: new[] { "Status", "OrganizationType" });

        // Step 7: Create indexes for Customers
        migrationBuilder.CreateIndex(
            name: "IX_Customers_OrganizationId_Role",
            table: "Customers",
            columns: new[] { "OrganizationId", "Role" });

        // Step 8: Add foreign key constraints
        migrationBuilder.AddForeignKey(
            name: "FK_Organizations_Customers_OwnerId",
            table: "Organizations",
            column: "OwnerId",
            principalTable: "Customers",
            principalColumn: "CustomerId",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_Customers_Organizations_OrganizationId",
            table: "Customers",
            column: "OrganizationId",
            principalTable: "Organizations",
            principalColumn: "OrganizationId",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_TeamInvitations_Organizations_OrganizationId",
            table: "TeamInvitations",
            column: "OrganizationId",
            principalTable: "Organizations",
            principalColumn: "OrganizationId",
            onDelete: ReferentialAction.Cascade);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Remove foreign key constraints
        migrationBuilder.DropForeignKey(
            name: "FK_TeamInvitations_Organizations_OrganizationId",
            table: "TeamInvitations");

        migrationBuilder.DropForeignKey(
            name: "FK_Customers_Organizations_OrganizationId",
            table: "Customers");

        migrationBuilder.DropForeignKey(
            name: "FK_Organizations_Customers_OwnerId",
            table: "Organizations");

        // Remove indexes
        migrationBuilder.DropIndex(
            name: "IX_Organizations_Name",
            table: "Organizations");

        migrationBuilder.DropIndex(
            name: "IX_Organizations_OwnerId",
            table: "Organizations");

        migrationBuilder.DropIndex(
            name: "IX_Organizations_Status_OrganizationType",
            table: "Organizations");

        migrationBuilder.DropIndex(
            name: "IX_Customers_OrganizationId_Role",
            table: "Customers");

        // Remove columns
        migrationBuilder.DropColumn(
            name: "InvitationMessage",
            table: "TeamInvitations");

        migrationBuilder.DropColumn(
            name: "OrganizationId",
            table: "Customers");

        migrationBuilder.DropColumn(
            name: "Role",
            table: "Customers");

        // Drop Organizations table
        migrationBuilder.DropTable(
            name: "Organizations");
    }
}