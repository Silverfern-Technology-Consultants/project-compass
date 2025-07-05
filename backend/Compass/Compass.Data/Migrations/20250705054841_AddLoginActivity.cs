using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Compass.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLoginActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LoginActivities",
                columns: table => new
                {
                    LoginActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoginTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LogoutTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DeviceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Browser = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OperatingSystem = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SessionId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Active"),
                    LastActivityTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LoginMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Password"),
                    MfaUsed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    SuspiciousActivity = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    SecurityNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginActivities", x => x.LoginActivityId);
                    table.ForeignKey(
                        name: "FK_LoginActivities_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoginActivities_CustomerId",
                table: "LoginActivities",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_LoginActivities_CustomerId_IsActive_Status",
                table: "LoginActivities",
                columns: new[] { "CustomerId", "IsActive", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LoginActivities_CustomerId_SessionId",
                table: "LoginActivities",
                columns: new[] { "CustomerId", "SessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_LoginActivities_IpAddress",
                table: "LoginActivities",
                column: "IpAddress");

            migrationBuilder.CreateIndex(
                name: "IX_LoginActivities_LoginTime",
                table: "LoginActivities",
                column: "LoginTime");

            migrationBuilder.CreateIndex(
                name: "IX_LoginActivities_SuspiciousActivity_LoginTime",
                table: "LoginActivities",
                columns: new[] { "SuspiciousActivity", "LoginTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoginActivities");
        }
    }
}
