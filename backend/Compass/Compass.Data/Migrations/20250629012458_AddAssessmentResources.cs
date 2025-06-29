using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Compass.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAssessmentResources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "OverallScore",
                table: "Assessments",
                type: "decimal(5,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "AssessmentResources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ResourceTypeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ResourceGroup = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SubscriptionId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Sku = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}"),
                    TagCount = table.Column<int>(type: "int", nullable: false),
                    Environment = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Properties = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentResources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssessmentResources_Assessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalTable: "Assessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentResources_AssessmentId",
                table: "AssessmentResources",
                column: "AssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentResources_AssessmentId_Location",
                table: "AssessmentResources",
                columns: new[] { "AssessmentId", "Location" });

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentResources_AssessmentId_ResourceGroup",
                table: "AssessmentResources",
                columns: new[] { "AssessmentId", "ResourceGroup" });

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentResources_AssessmentId_ResourceTypeName",
                table: "AssessmentResources",
                columns: new[] { "AssessmentId", "ResourceTypeName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssessmentResources");

            migrationBuilder.AlterColumn<decimal>(
                name: "OverallScore",
                table: "Assessments",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)",
                oldNullable: true);
        }
    }
}
