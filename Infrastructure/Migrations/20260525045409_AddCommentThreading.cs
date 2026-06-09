using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CargoInbox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommentThreading : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ParentCommentId",
                table: "MailComments",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: "default",
                column: "CreatedAt",
                value: new DateTime(2026, 5, 25, 4, 54, 7, 736, DateTimeKind.Utc).AddTicks(5221));

            migrationBuilder.CreateIndex(
                name: "IX_MailComments_ParentCommentId",
                table: "MailComments",
                column: "ParentCommentId");

            migrationBuilder.AddForeignKey(
                name: "FK_MailComments_MailComments_ParentCommentId",
                table: "MailComments",
                column: "ParentCommentId",
                principalTable: "MailComments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MailComments_MailComments_ParentCommentId",
                table: "MailComments");

            migrationBuilder.DropIndex(
                name: "IX_MailComments_ParentCommentId",
                table: "MailComments");

            migrationBuilder.DropColumn(
                name: "ParentCommentId",
                table: "MailComments");

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: "default",
                column: "CreatedAt",
                value: new DateTime(2026, 5, 23, 11, 23, 6, 576, DateTimeKind.Utc).AddTicks(5620));
        }
    }
}
