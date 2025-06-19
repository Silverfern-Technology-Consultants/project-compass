using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Compass.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Assessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OverallScore = table.Column<int>(type: "int", nullable: false),
                    StartedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReportBlobUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assessments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssessmentFindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    ResourceId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ResourceName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ResourceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Issue = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Recommendation = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    EstimatedEffort = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentFindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssessmentFindings_Assessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalTable: "Assessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentFindings_AssessmentId",
                table: "AssessmentFindings",
                column: "AssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentFindings_Category",
                table: "AssessmentFindings",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentFindings_Severity",
                table: "AssessmentFindings",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_CustomerId",
                table: "Assessments",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_StartedDate",
                table: "Assessments",
                column: "StartedDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssessmentFindings");

            migrationBuilder.DropTable(
                name: "Assessments");
        }
    }
}
