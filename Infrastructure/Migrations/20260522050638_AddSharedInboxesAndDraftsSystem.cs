using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CargoInbox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedInboxesAndDraftsSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SharedInboxId",
                table: "Conversations",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ConversationDrafts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ConversationId = table.Column<string>(type: "text", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: false),
                    CreatedByUserName = table.Column<string>(type: "text", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    TextBody = table.Column<string>(type: "text", nullable: false),
                    HtmlBody = table.Column<string>(type: "text", nullable: false),
                    IsLockedForApproval = table.Column<bool>(type: "boolean", nullable: false),
                    ApprovedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationDrafts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SharedInboxes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    EmailAddress = table.Column<string>(type: "text", nullable: false),
                    SmtpHost = table.Column<string>(type: "text", nullable: false),
                    SmtpPort = table.Column<int>(type: "integer", nullable: false),
                    ImapHost = table.Column<string>(type: "text", nullable: false),
                    ImapPort = table.Column<int>(type: "integer", nullable: false),
                    EncryptedPassword = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedInboxes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserInboxPermissions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    SharedInboxId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserInboxPermissions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_SharedInboxId",
                table: "Conversations",
                column: "SharedInboxId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationDrafts_ConversationId",
                table: "ConversationDrafts",
                column: "ConversationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SharedInboxes_EmailAddress",
                table: "SharedInboxes",
                column: "EmailAddress",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserInboxPermissions_UserId_SharedInboxId",
                table: "UserInboxPermissions",
                columns: new[] { "UserId", "SharedInboxId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_SharedInboxes_SharedInboxId",
                table: "Conversations",
                column: "SharedInboxId",
                principalTable: "SharedInboxes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_SharedInboxes_SharedInboxId",
                table: "Conversations");

            migrationBuilder.DropTable(
                name: "ConversationDrafts");

            migrationBuilder.DropTable(
                name: "SharedInboxes");

            migrationBuilder.DropTable(
                name: "UserInboxPermissions");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_SharedInboxId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "SharedInboxId",
                table: "Conversations");
        }
    }
}
