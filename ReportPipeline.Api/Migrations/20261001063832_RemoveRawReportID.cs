using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReportPipeline.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRawReportID : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReportId",
                table: "Raw");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReportId",
                table: "Raw",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
