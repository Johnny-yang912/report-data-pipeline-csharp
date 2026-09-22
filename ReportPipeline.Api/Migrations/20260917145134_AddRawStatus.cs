using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReportPipeline.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRawStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Raw",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "Raw");
        }
    }
}
