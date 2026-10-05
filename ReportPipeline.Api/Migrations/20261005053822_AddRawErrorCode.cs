using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReportPipeline.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRawErrorCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ErrorCode",
                table: "Raw",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ErrorCode",
                table: "Raw");
        }
    }
}
