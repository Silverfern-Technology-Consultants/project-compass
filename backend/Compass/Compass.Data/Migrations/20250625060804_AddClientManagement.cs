using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Compass.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClientManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                table: "Subscriptions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                table: "AzureEnvironments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                table: "Assessments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Industry = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ContactName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ContactEmail = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ContactPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Active"),
                    TimeZone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Settings = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContractStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ContractEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByCustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedByCustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.ClientId);
                    table.ForeignKey(
                        name: "FK_Clients_Customers_CreatedByCustomerId",
                        column: x => x.CreatedByCustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Clients_Customers_LastModifiedByCustomerId",
                        column: x => x.LastModifiedByCustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Clients_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "OrganizationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClientAccess",
                columns: table => new
                {
                    ClientAccessId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccessLevel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Read"),
                    CanViewAssessments = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CanCreateAssessments = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CanDeleteAssessments = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CanManageEnvironments = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CanViewReports = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CanExportData = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GrantedByCustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientAccess", x => x.ClientAccessId);
                    table.ForeignKey(
                        name: "FK_ClientAccess_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "ClientId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientAccess_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientAccess_Customers_GrantedByCustomerId",
                        column: x => x.GrantedByCustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_ClientId",
                table: "Subscriptions",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_AzureEnvironments_ClientId",
                table: "AzureEnvironments",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_ClientId",
                table: "Assessments",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientAccess_ClientId",
                table: "ClientAccess",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientAccess_CustomerId",
                table: "ClientAccess",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientAccess_CustomerId_ClientId",
                table: "ClientAccess",
                columns: new[] { "CustomerId", "ClientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientAccess_GrantedByCustomerId",
                table: "ClientAccess",
                column: "GrantedByCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_CreatedByCustomerId",
                table: "Clients",
                column: "CreatedByCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_LastModifiedByCustomerId",
                table: "Clients",
                column: "LastModifiedByCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_Name_OrganizationId",
                table: "Clients",
                columns: new[] { "Name", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Clients_OrganizationId",
                table: "Clients",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_Status_IsActive",
                table: "Clients",
                columns: new[] { "Status", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_Assessments_Clients_ClientId",
                table: "Assessments",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "ClientId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AzureEnvironments_Clients_ClientId",
                table: "AzureEnvironments",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "ClientId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Subscriptions_Clients_ClientId",
                table: "Subscriptions",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "ClientId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Assessments_Clients_ClientId",
                table: "Assessments");

            migrationBuilder.DropForeignKey(
                name: "FK_AzureEnvironments_Clients_ClientId",
                table: "AzureEnvironments");

            migrationBuilder.DropForeignKey(
                name: "FK_Subscriptions_Clients_ClientId",
                table: "Subscriptions");

            migrationBuilder.DropTable(
                name: "ClientAccess");

            migrationBuilder.DropTable(
                name: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_ClientId",
                table: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_AzureEnvironments_ClientId",
                table: "AzureEnvironments");

            migrationBuilder.DropIndex(
                name: "IX_Assessments_ClientId",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "AzureEnvironments");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "Assessments");
        }
    }
}
