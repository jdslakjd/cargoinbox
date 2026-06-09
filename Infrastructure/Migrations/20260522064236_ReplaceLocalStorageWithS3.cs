using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CargoInbox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceLocalStorageWithS3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "StorageKey",
                table: "Attachments",
                newName: "FileUrl");

            migrationBuilder.AddColumn<string>(
                name: "FilePath",
                table: "Attachments",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FilePath",
                table: "Attachments");

            migrationBuilder.RenameColumn(
                name: "FileUrl",
                table: "Attachments",
                newName: "StorageKey");
        }
    }
}
