using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace topg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddStartObscuredToImageQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "StartObscured",
                table: "Questions",
                type: "boolean",
                nullable: true,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StartObscured",
                table: "Questions");
        }
    }
}
