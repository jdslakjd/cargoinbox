using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CargoInbox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FullTenantIsolation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "UserSignatures",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "UserMailConfigs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "UserInboxPermissions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "TeamGroups",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "SlaPolicies",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "SequenceSteps",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "SequenceExecutions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "SavedViews",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "RuleConditions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "Notifications",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "MessageTemplates",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "MessageApprovals",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "Mails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "EmailSequences",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "Drafts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "Customers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "AutomationRules",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "Attachments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "ApprovalRules",
                type: "text",
                nullable: false,
                defaultValue: "");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "UserSignatures");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "UserMailConfigs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "UserInboxPermissions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "TeamGroups");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "SlaPolicies");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "SequenceSteps");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "SequenceExecutions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "SavedViews");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "RuleConditions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "MessageTemplates");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "MessageApprovals");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Mails");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "EmailSequences");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Drafts");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AutomationRules");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ApprovalRules");
        }
    }
}
