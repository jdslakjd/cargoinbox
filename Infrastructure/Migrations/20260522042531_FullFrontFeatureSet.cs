using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CargoInbox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FullFrontFeatureSet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultSignatureId",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstRespondedAt",
                table: "Conversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAt",
                table: "Conversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SlaBreachAt",
                table: "Conversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SnoozedUntil",
                table: "Conversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ActivityLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ConversationId = table.Column<string>(type: "text", nullable: true),
                    MailId = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    UserName = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    Detail = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Drafts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ConversationId = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ToAddress = table.Column<string>(type: "text", nullable: false),
                    CcAddress = table.Column<string>(type: "text", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    HtmlBody = table.Column<string>(type: "text", nullable: false),
                    TextBody = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drafts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MessageTemplates",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    TeamId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    IsShared = table.Column<bool>(type: "boolean", nullable: false),
                    UsageCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    LinkUrl = table.Column<string>(type: "text", nullable: true),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SavedViews",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    FilterJson = table.Column<string>(type: "text", nullable: false),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedViews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SlaPolicies",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    FirstResponseMinutes = table.Column<int>(type: "integer", nullable: false),
                    ResolutionMinutes = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlaPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TeamGroups",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    TeamId = table.Column<string>(type: "text", nullable: false),
                    MemberUserIds = table.Column<List<string>>(type: "text[]", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserSignatures",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    HtmlContent = table.Column<string>(type: "text", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSignatures", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_SlaBreachAt",
                table: "Conversations",
                column: "SlaBreachAt");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_SnoozedUntil",
                table: "Conversations",
                column: "SnoozedUntil");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_ConversationId",
                table: "ActivityLogs",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_CreatedAt",
                table: "ActivityLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Drafts_ConversationId",
                table: "Drafts",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_Drafts_UserId",
                table: "Drafts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageTemplates_TeamId",
                table: "MessageTemplates",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageTemplates_UserId",
                table: "MessageTemplates",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedViews_UserId",
                table: "SavedViews",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamGroups_TeamId",
                table: "TeamGroups",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSignatures_UserId",
                table: "UserSignatures",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityLogs");

            migrationBuilder.DropTable(
                name: "Drafts");

            migrationBuilder.DropTable(
                name: "MessageTemplates");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "SavedViews");

            migrationBuilder.DropTable(
                name: "SlaPolicies");

            migrationBuilder.DropTable(
                name: "TeamGroups");

            migrationBuilder.DropTable(
                name: "UserSignatures");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_SlaBreachAt",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_SnoozedUntil",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "DefaultSignatureId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "FirstRespondedAt",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "SlaBreachAt",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "SnoozedUntil",
                table: "Conversations");
        }
    }
}
