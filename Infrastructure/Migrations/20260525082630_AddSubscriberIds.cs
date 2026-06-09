using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CargoInbox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriberIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "SubscriberIds",
                table: "Conversations",
                type: "text[]",
                nullable: false,
                defaultValue: new List<string>());

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: "default",
                column: "CreatedAt",
                value: new DateTime(2026, 5, 25, 8, 26, 30, 241, DateTimeKind.Utc).AddTicks(9937));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubscriberIds",
                table: "Conversations");

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: "default",
                column: "CreatedAt",
                value: new DateTime(2026, 5, 25, 7, 0, 23, 6, DateTimeKind.Utc).AddTicks(9105));
        }
    }
}
