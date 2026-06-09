using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CargoInbox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ThreeCriticalGapPatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConsecutiveFailureCount",
                table: "UserMailConfigs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsSuspended",
                table: "UserMailConfigs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SuspendedUntil",
                table: "UserMailConfigs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConditionsJson",
                table: "AutomationRules",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ApprovalRules",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    RequesterUserId = table.Column<string>(type: "text", nullable: false),
                    ApproverUserId = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MessageApprovals",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ConversationId = table.Column<string>(type: "text", nullable: false),
                    RequesterUserId = table.Column<string>(type: "text", nullable: false),
                    ApproverUserId = table.Column<string>(type: "text", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    HtmlBody = table.Column<string>(type: "text", nullable: false),
                    TextBody = table.Column<string>(type: "text", nullable: true),
                    CcAddress = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RejectionReason = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageApprovals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RuleConditions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    RuleId = table.Column<string>(type: "text", nullable: false),
                    Field = table.Column<int>(type: "integer", nullable: false),
                    Operator = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    LogicGate = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleConditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RuleConditions_AutomationRules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "AutomationRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "AutomationRules",
                keyColumn: "Id",
                keyValue: "rule-1",
                column: "ConditionsJson",
                value: "[]");

            migrationBuilder.UpdateData(
                table: "AutomationRules",
                keyColumn: "Id",
                keyValue: "rule-2",
                column: "ConditionsJson",
                value: "[]");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRules_RequesterUserId",
                table: "ApprovalRules",
                column: "RequesterUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessageApprovals_RequesterUserId",
                table: "MessageApprovals",
                column: "RequesterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageApprovals_Status",
                table: "MessageApprovals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RuleConditions_RuleId",
                table: "RuleConditions",
                column: "RuleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovalRules");

            migrationBuilder.DropTable(
                name: "MessageApprovals");

            migrationBuilder.DropTable(
                name: "RuleConditions");

            migrationBuilder.DropColumn(
                name: "ConsecutiveFailureCount",
                table: "UserMailConfigs");

            migrationBuilder.DropColumn(
                name: "IsSuspended",
                table: "UserMailConfigs");

            migrationBuilder.DropColumn(
                name: "SuspendedUntil",
                table: "UserMailConfigs");

            migrationBuilder.DropColumn(
                name: "ConditionsJson",
                table: "AutomationRules");
        }
    }
}
