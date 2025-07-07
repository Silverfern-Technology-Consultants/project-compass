using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Compass.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAssessmentCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "AssessmentType",
                table: "Assessments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "AssessmentCategory",
                table: "Assessments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "ResourceGovernance");

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_AssessmentCategory",
                table: "Assessments",
                column: "AssessmentCategory");

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_Category_Type",
                table: "Assessments",
                columns: new[] { "AssessmentCategory", "AssessmentType" });

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_Organization_Category",
                table: "Assessments",
                columns: new[] { "OrganizationId", "AssessmentCategory" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Assessments_AssessmentCategory",
                table: "Assessments");

            migrationBuilder.DropIndex(
                name: "IX_Assessments_Category_Type",
                table: "Assessments");

            migrationBuilder.DropIndex(
                name: "IX_Assessments_Organization_Category",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "AssessmentCategory",
                table: "Assessments");

            migrationBuilder.AlterColumn<string>(
                name: "AssessmentType",
                table: "Assessments",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);
        }
    }
}
