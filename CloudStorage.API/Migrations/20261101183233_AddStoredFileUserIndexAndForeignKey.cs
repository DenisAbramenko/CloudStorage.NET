using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudStorage.API.Migrations
{
    /// <inheritdoc />
    public partial class AddStoredFileUserIndexAndForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_UserId_CreatedAtUtc_Id",
                table: "StoredFiles",
                columns: new[] { "UserId", "CreatedAtUtc", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_StoredFiles_AspNetUsers_UserId",
                table: "StoredFiles",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StoredFiles_AspNetUsers_UserId",
                table: "StoredFiles");

            migrationBuilder.DropIndex(
                name: "IX_StoredFiles_UserId_CreatedAtUtc_Id",
                table: "StoredFiles");
        }
    }
}
