using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CargoInbox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantChannelConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantChannelConfigs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    WhatsAppPhoneNumberId = table.Column<string>(type: "text", nullable: true),
                    WhatsAppAccessToken = table.Column<string>(type: "text", nullable: true),
                    FacebookPageAccessToken = table.Column<string>(type: "text", nullable: true),
                    FacebookPageId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantChannelConfigs", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: "default",
                column: "CreatedAt",
                value: new DateTime(2026, 6, 9, 9, 14, 57, 949, DateTimeKind.Utc).AddTicks(6881));

            migrationBuilder.CreateIndex(
                name: "IX_TenantChannelConfigs_TenantId",
                table: "TenantChannelConfigs",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantChannelConfigs");

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: "default",
                column: "CreatedAt",
                value: new DateTime(2026, 5, 25, 8, 26, 30, 241, DateTimeKind.Utc).AddTicks(9937));
        }
    }
}
