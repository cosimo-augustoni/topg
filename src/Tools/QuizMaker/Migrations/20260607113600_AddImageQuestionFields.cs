using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuizMaker.Migrations
{
    /// <inheritdoc />
    public partial class AddImageQuestionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename rather than drop/add so existing image filenames are preserved.
            migrationBuilder.RenameColumn(
                name: "ImageUri",
                table: "Questions",
                newName: "QuestionImageUri");

            migrationBuilder.AddColumn<string>(
                name: "AnswerText",
                table: "Questions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnswerImageUri",
                table: "Questions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ImageSize",
                table: "Questions",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnswerText",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "AnswerImageUri",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "ImageSize",
                table: "Questions");

            migrationBuilder.RenameColumn(
                name: "QuestionImageUri",
                table: "Questions",
                newName: "ImageUri");
        }
    }
}
