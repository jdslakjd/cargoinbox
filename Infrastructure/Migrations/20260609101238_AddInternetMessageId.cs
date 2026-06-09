using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CargoInbox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInternetMessageId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InternetMessageId",
                table: "ConversationMessages",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: "default",
                column: "CreatedAt",
                value: new DateTime(2026, 6, 9, 10, 12, 38, 472, DateTimeKind.Utc).AddTicks(2956));

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_TenantId_InternetMessageId",
                table: "ConversationMessages",
                columns: new[] { "TenantId", "InternetMessageId" },
                filter: "\"InternetMessageId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ConversationMessages_TenantId_InternetMessageId",
                table: "ConversationMessages");

            migrationBuilder.DropColumn(
                name: "InternetMessageId",
                table: "ConversationMessages");

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: "default",
                column: "CreatedAt",
                value: new DateTime(2026, 6, 9, 9, 14, 57, 949, DateTimeKind.Utc).AddTicks(6881));
        }
    }
}
