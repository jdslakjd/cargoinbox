using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CargoInbox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLiveChatWidgetAndSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contacts_TenantId",
                table: "Contacts");

            migrationBuilder.AddColumn<string>(
                name: "LiveChatVisitorId",
                table: "Contacts",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LiveChatWidgets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    PublicKey = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    WelcomeMessage = table.Column<string>(type: "text", nullable: false),
                    OfflineMessage = table.Column<string>(type: "text", nullable: false),
                    PrimaryColor = table.Column<string>(type: "text", nullable: false),
                    Position = table.Column<string>(type: "text", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiveChatWidgets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LiveChatSessions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    WidgetId = table.Column<string>(type: "text", nullable: false),
                    VisitorId = table.Column<string>(type: "text", nullable: false),
                    ContactId = table.Column<string>(type: "text", nullable: false),
                    ConversationId = table.Column<string>(type: "text", nullable: false),
                    VisitorName = table.Column<string>(type: "text", nullable: true),
                    VisitorEmail = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastActiveAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiveChatSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LiveChatSessions_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LiveChatSessions_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LiveChatSessions_LiveChatWidgets_WidgetId",
                        column: x => x.WidgetId,
                        principalTable: "LiveChatWidgets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: "default",
                column: "CreatedAt",
                value: new DateTime(2026, 6, 9, 11, 14, 14, 45, DateTimeKind.Utc).AddTicks(6298));

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_TenantId_LiveChatVisitorId",
                table: "Contacts",
                columns: new[] { "TenantId", "LiveChatVisitorId" });

            migrationBuilder.CreateIndex(
                name: "IX_LiveChatSessions_ContactId",
                table: "LiveChatSessions",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_LiveChatSessions_ConversationId",
                table: "LiveChatSessions",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_LiveChatSessions_WidgetId_VisitorId",
                table: "LiveChatSessions",
                columns: new[] { "WidgetId", "VisitorId" });

            migrationBuilder.CreateIndex(
                name: "IX_LiveChatWidgets_PublicKey",
                table: "LiveChatWidgets",
                column: "PublicKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LiveChatWidgets_TenantId",
                table: "LiveChatWidgets",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LiveChatSessions");

            migrationBuilder.DropTable(
                name: "LiveChatWidgets");

            migrationBuilder.DropIndex(
                name: "IX_Contacts_TenantId_LiveChatVisitorId",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "LiveChatVisitorId",
                table: "Contacts");

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: "default",
                column: "CreatedAt",
                value: new DateTime(2026, 6, 9, 11, 6, 32, 341, DateTimeKind.Utc).AddTicks(4024));

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_TenantId",
                table: "Contacts",
                column: "TenantId");
        }
    }
}
