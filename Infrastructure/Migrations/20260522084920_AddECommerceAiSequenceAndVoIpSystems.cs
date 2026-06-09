using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CargoInbox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddECommerceAiSequenceAndVoIpSystems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CallLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ContactId = table.Column<string>(type: "text", nullable: false),
                    ConversationId = table.Column<string>(type: "text", nullable: false),
                    PhoneNumber = table.Column<string>(type: "text", nullable: false),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    RecordingUrl = table.Column<string>(type: "text", nullable: false),
                    AudioToTextTranscript = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CallLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SequenceTrackers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    ContactId = table.Column<string>(type: "text", nullable: false),
                    CurrentStep = table.Column<string>(type: "text", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    NextTriggerTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SequenceTrackers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShopifyOrders",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    CustomerEmail = table.Column<string>(type: "text", nullable: false),
                    OrderNumber = table.Column<string>(type: "text", nullable: false),
                    TotalPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    FulfillmentStatus = table.Column<string>(type: "text", nullable: false),
                    TrackingNumber = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopifyOrders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CallLogs_ContactId",
                table: "CallLogs",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_SequenceTrackers_ContactId_IsCompleted",
                table: "SequenceTrackers",
                columns: new[] { "ContactId", "IsCompleted" });

            migrationBuilder.CreateIndex(
                name: "IX_ShopifyOrders_CustomerEmail",
                table: "ShopifyOrders",
                column: "CustomerEmail");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CallLogs");

            migrationBuilder.DropTable(
                name: "SequenceTrackers");

            migrationBuilder.DropTable(
                name: "ShopifyOrders");
        }
    }
}
