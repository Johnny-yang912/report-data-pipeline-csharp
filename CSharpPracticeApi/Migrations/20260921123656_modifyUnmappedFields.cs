using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSharpPracticeApi.Migrations
{
    /// <inheritdoc />
    public partial class modifyUnmappedFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UmappedFields",
                table: "Reports",
                newName: "UnmappedFields");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UnmappedFields",
                table: "Reports",
                newName: "UmappedFields");
        }
    }
}
