using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CargoInbox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FinalEntityAuditFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AutomationRules",
                keyColumn: "Id",
                keyValue: "rule-1",
                column: "TenantId",
                value: "default");

            migrationBuilder.UpdateData(
                table: "AutomationRules",
                keyColumn: "Id",
                keyValue: "rule-2",
                column: "TenantId",
                value: "default");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AutomationRules",
                keyColumn: "Id",
                keyValue: "rule-1",
                column: "TenantId",
                value: "");

            migrationBuilder.UpdateData(
                table: "AutomationRules",
                keyColumn: "Id",
                keyValue: "rule-2",
                column: "TenantId",
                value: "");
        }
    }
}
