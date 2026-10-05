using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReportPipeline.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRawSourceClientId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceClientId",
                table: "Raw",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceClientId",
                table: "Raw");
        }
    }
}
