using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Compass.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeamInvitations",
                columns: table => new
                {
                    InvitationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvitedEmail = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    InvitedRole = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    InvitationToken = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    InvitedByCustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvitedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Pending"),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcceptedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcceptedByCustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamInvitations", x => x.InvitationId);
                    table.ForeignKey(
                        name: "FK_TeamInvitations_Customers_AcceptedByCustomerId",
                        column: x => x.AcceptedByCustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamInvitations_Customers_InvitedByCustomerId",
                        column: x => x.InvitedByCustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeamInvitations_AcceptedByCustomerId",
                table: "TeamInvitations",
                column: "AcceptedByCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamInvitations_ExpirationDate",
                table: "TeamInvitations",
                column: "ExpirationDate");

            migrationBuilder.CreateIndex(
                name: "IX_TeamInvitations_InvitationToken",
                table: "TeamInvitations",
                column: "InvitationToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamInvitations_InvitedByCustomerId",
                table: "TeamInvitations",
                column: "InvitedByCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamInvitations_InvitedEmail",
                table: "TeamInvitations",
                column: "InvitedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_TeamInvitations_OrganizationId_Status",
                table: "TeamInvitations",
                columns: new[] { "OrganizationId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeamInvitations");
        }
    }
}
