using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Compass.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClientPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientPreferences",
                columns: table => new
                {
                    ClientPreferencesId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllowedNamingPatterns = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequiredNamingElements = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EnvironmentIndicators = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    RequiredTags = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EnforceTagCompliance = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ComplianceFrameworks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedByCustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedByCustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientPreferences", x => x.ClientPreferencesId);
                    table.ForeignKey(
                        name: "FK_ClientPreferences_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "ClientId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientPreferences_Customers_CreatedByCustomerId",
                        column: x => x.CreatedByCustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientPreferences_Customers_LastModifiedByCustomerId",
                        column: x => x.LastModifiedByCustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientPreferences_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "OrganizationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientPreferences_ClientId",
                table: "ClientPreferences",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientPreferences_ClientId_OrganizationId",
                table: "ClientPreferences",
                columns: new[] { "ClientId", "OrganizationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientPreferences_CreatedByCustomerId",
                table: "ClientPreferences",
                column: "CreatedByCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientPreferences_IsActive_OrganizationId",
                table: "ClientPreferences",
                columns: new[] { "IsActive", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientPreferences_LastModifiedByCustomerId",
                table: "ClientPreferences",
                column: "LastModifiedByCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientPreferences_OrganizationId",
                table: "ClientPreferences",
                column: "OrganizationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientPreferences");
        }
    }
}
