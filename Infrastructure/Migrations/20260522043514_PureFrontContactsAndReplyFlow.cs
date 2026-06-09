using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CargoInbox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PureFrontContactsAndReplyFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContactId",
                table: "Conversations",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Contacts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Phone = table.Column<string>(type: "text", nullable: true),
                    Company = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contacts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_ContactId",
                table: "Conversations",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_Email",
                table: "Contacts",
                column: "Email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Contacts_ContactId",
                table: "Conversations",
                column: "ContactId",
                principalTable: "Contacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Contacts_ContactId",
                table: "Conversations");

            migrationBuilder.DropTable(
                name: "Contacts");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_ContactId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "ContactId",
                table: "Conversations");
        }
    }
}
