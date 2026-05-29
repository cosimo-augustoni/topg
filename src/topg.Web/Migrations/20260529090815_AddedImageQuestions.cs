using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace topg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddedImageQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUri",
                table: "Questions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TextQuestion_CorrectAnswer",
                table: "Questions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TextQuestion_QuestionText",
                table: "Questions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUri",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "TextQuestion_CorrectAnswer",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "TextQuestion_QuestionText",
                table: "Questions");
        }
    }
}
