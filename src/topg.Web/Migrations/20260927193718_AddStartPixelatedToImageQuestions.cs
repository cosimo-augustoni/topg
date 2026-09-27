using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace topg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddStartPixelatedToImageQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "StartPixelated",
                table: "Questions",
                type: "boolean",
                nullable: true,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StartPixelated",
                table: "Questions");
        }
    }
}
