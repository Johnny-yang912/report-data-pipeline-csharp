using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReportPipeline.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddReportsRawIdFK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Reports_RawId",
                table: "Reports",
                column: "RawId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Reports_Raw_RawId",
                table: "Reports",
                column: "RawId",
                principalTable: "Raw",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reports_Raw_RawId",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_Reports_RawId",
                table: "Reports");
        }
    }
}
