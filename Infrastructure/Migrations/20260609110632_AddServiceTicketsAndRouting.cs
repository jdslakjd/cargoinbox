using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CargoInbox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceTicketsAndRouting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoutingQueueCursors",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    ScopeKey = table.Column<string>(type: "text", nullable: false),
                    LastAssignedUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoutingQueueCursors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServiceTickets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    ConversationId = table.Column<string>(type: "text", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    ContactId = table.Column<string>(type: "text", nullable: true),
                    AssignedToUserId = table.Column<string>(type: "text", nullable: true),
                    AssignedToUserName = table.Column<string>(type: "text", nullable: true),
                    SharedInboxId = table.Column<string>(type: "text", nullable: true),
                    TeamGroupId = table.Column<string>(type: "text", nullable: true),
                    FirstResponseAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsSlaBreached = table.Column<bool>(type: "boolean", nullable: false),
                    Tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceTickets_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ServiceTickets_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: "default",
                column: "CreatedAt",
                value: new DateTime(2026, 6, 9, 11, 6, 32, 341, DateTimeKind.Utc).AddTicks(4024));

            migrationBuilder.CreateIndex(
                name: "IX_RoutingQueueCursors_TenantId_ScopeKey",
                table: "RoutingQueueCursors",
                columns: new[] { "TenantId", "ScopeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTickets_AssignedToUserId",
                table: "ServiceTickets",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTickets_ContactId",
                table: "ServiceTickets",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTickets_ConversationId",
                table: "ServiceTickets",
                column: "ConversationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTickets_IsSlaBreached",
                table: "ServiceTickets",
                column: "IsSlaBreached");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTickets_Status",
                table: "ServiceTickets",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTickets_TenantId_Number",
                table: "ServiceTickets",
                columns: new[] { "TenantId", "Number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoutingQueueCursors");

            migrationBuilder.DropTable(
                name: "ServiceTickets");

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: "default",
                column: "CreatedAt",
                value: new DateTime(2026, 6, 9, 11, 1, 33, 375, DateTimeKind.Utc).AddTicks(5810));
        }
    }
}
