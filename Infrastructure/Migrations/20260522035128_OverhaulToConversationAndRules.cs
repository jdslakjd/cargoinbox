using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CargoInbox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OverhaulToConversationAndRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "MailId",
                table: "MailComments",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "ConversationId",
                table: "MailComments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConversationId1",
                table: "MailComments",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AutomationRules",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ConditionKeyword = table.Column<string>(type: "text", nullable: false),
                    ActionType = table.Column<string>(type: "text", nullable: false),
                    ActionValue = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Conversations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AssignedToUserId = table.Column<string>(type: "text", nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastMessageAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Labels = table.Column<List<string>>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConversationMessages",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ConversationId = table.Column<string>(type: "text", nullable: false),
                    FromAddress = table.Column<string>(type: "text", nullable: false),
                    ToAddress = table.Column<string>(type: "text", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    TextBody = table.Column<string>(type: "text", nullable: false),
                    HtmlBody = table.Column<string>(type: "text", nullable: false),
                    DateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    Embedding = table.Column<Vector>(type: "vector(1536)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationMessages_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "AutomationRules",
                columns: new[] { "Id", "ActionType", "ActionValue", "ConditionKeyword", "IsActive", "Name" },
                values: new object[,]
                {
                    { "rule-1", "Label", "🔥高优先级", "Urgent", true, "紧急邮件自动打标" },
                    { "rule-2", "Assign", "kate", "报价", true, "报价咨询自动指派" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_MailComments_ConversationId",
                table: "MailComments",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_MailComments_ConversationId1",
                table: "MailComments",
                column: "ConversationId1");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_ConversationId",
                table: "ConversationMessages",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_Embedding",
                table: "ConversationMessages",
                column: "Embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_AssignedToUserId",
                table: "Conversations",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_Status",
                table: "Conversations",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_MailComments_Conversations_ConversationId",
                table: "MailComments",
                column: "ConversationId",
                principalTable: "Conversations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MailComments_Conversations_ConversationId1",
                table: "MailComments",
                column: "ConversationId1",
                principalTable: "Conversations",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MailComments_Conversations_ConversationId",
                table: "MailComments");

            migrationBuilder.DropForeignKey(
                name: "FK_MailComments_Conversations_ConversationId1",
                table: "MailComments");

            migrationBuilder.DropTable(
                name: "AutomationRules");

            migrationBuilder.DropTable(
                name: "ConversationMessages");

            migrationBuilder.DropTable(
                name: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_MailComments_ConversationId",
                table: "MailComments");

            migrationBuilder.DropIndex(
                name: "IX_MailComments_ConversationId1",
                table: "MailComments");

            migrationBuilder.DropColumn(
                name: "ConversationId",
                table: "MailComments");

            migrationBuilder.DropColumn(
                name: "ConversationId1",
                table: "MailComments");

            migrationBuilder.AlterColumn<string>(
                name: "MailId",
                table: "MailComments",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
