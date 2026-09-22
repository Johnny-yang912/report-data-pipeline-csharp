using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSharpPracticeApi.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Raw",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<DateTime>(
                name: "ClaimedAt",
                table: "Raw",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Raw_Status_ClaimedAt",
                table: "Raw",
                columns: new[] { "Status", "ClaimedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Raw_Status_ClaimedAt",
                table: "Raw");

            migrationBuilder.DropColumn(
                name: "ClaimedAt",
                table: "Raw");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Raw",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
