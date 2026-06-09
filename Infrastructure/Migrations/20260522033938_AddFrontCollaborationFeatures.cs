using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CargoInbox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFrontCollaborationFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CommentText",
                table: "MailComments",
                newName: "UserName");

            migrationBuilder.AddColumn<DateTime>(
                name: "AssignedAt",
                table: "Mails",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedToUserId",
                table: "Mails",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Content",
                table: "MailComments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Mails_AssignedToUserId",
                table: "Mails",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Mails_Status",
                table: "Mails",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MailComments_MailId",
                table: "MailComments",
                column: "MailId");

            migrationBuilder.AddForeignKey(
                name: "FK_MailComments_Mails_MailId",
                table: "MailComments",
                column: "MailId",
                principalTable: "Mails",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MailComments_Mails_MailId",
                table: "MailComments");

            migrationBuilder.DropIndex(
                name: "IX_Mails_AssignedToUserId",
                table: "Mails");

            migrationBuilder.DropIndex(
                name: "IX_Mails_Status",
                table: "Mails");

            migrationBuilder.DropIndex(
                name: "IX_MailComments_MailId",
                table: "MailComments");

            migrationBuilder.DropColumn(
                name: "AssignedAt",
                table: "Mails");

            migrationBuilder.DropColumn(
                name: "AssignedToUserId",
                table: "Mails");

            migrationBuilder.DropColumn(
                name: "Content",
                table: "MailComments");

            migrationBuilder.RenameColumn(
                name: "UserName",
                table: "MailComments",
                newName: "CommentText");
        }
    }
}
