using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace topg.Web.Migrations
{
    /// <inheritdoc />
    public partial class LinkQuationsToBoard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "BoardId",
                table: "Questions",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Questions_BoardId",
                table: "Questions",
                column: "BoardId");

            migrationBuilder.AddForeignKey(
                name: "FK_Questions_Boards_BoardId",
                table: "Questions",
                column: "BoardId",
                principalTable: "Boards",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Questions_Boards_BoardId",
                table: "Questions");

            migrationBuilder.DropIndex(
                name: "IX_Questions_BoardId",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "BoardId",
                table: "Questions");
        }
    }
}
