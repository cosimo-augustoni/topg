using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace topg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddedAdditionalInformationToImageQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TextQuestion_CorrectAnswer",
                table: "Questions",
                newName: "QuestionImageUri");

            migrationBuilder.RenameColumn(
                name: "ImageUri",
                table: "Questions",
                newName: "AnswerText");

            migrationBuilder.AddColumn<string>(
                name: "AnswerImageUri",
                table: "Questions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ImageSize",
                table: "Questions",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnswerImageUri",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "ImageSize",
                table: "Questions");

            migrationBuilder.RenameColumn(
                name: "QuestionImageUri",
                table: "Questions",
                newName: "TextQuestion_CorrectAnswer");

            migrationBuilder.RenameColumn(
                name: "AnswerText",
                table: "Questions",
                newName: "ImageUri");
        }
    }
}
