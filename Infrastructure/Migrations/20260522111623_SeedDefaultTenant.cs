using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CargoInbox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedDefaultTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Tenants",
                columns: new[] { "Id", "CreatedAt", "Domain", "IsActive", "Name" },
                values: new object[] { "default", new DateTime(2026, 5, 22, 11, 16, 22, 829, DateTimeKind.Utc).AddTicks(3889), "cargoinbox.cn", true, "默认租户" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: "default");
        }
    }
}
