using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CargoInbox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantIsolation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Username",
                table: "Users");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "SharedInboxes",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "MailComments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "Conversations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "ConversationMessages",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "ConversationDrafts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "Contacts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "ActivityLogs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Domain = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId_Username",
                table: "Users",
                columns: new[] { "TenantId", "Username" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_TenantId",
                table: "Conversations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_TenantId",
                table: "Contacts",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Contacts_Tenants_TenantId",
                table: "Contacts",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Tenants_TenantId",
                table: "Conversations",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Tenants_TenantId",
                table: "Users",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contacts_Tenants_TenantId",
                table: "Contacts");

            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Tenants_TenantId",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Tenants_TenantId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Users_TenantId_Username",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_TenantId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Contacts_TenantId",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "SharedInboxes");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "MailComments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ConversationMessages");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ConversationDrafts");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ActivityLogs");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }
    }
}
