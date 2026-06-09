using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CargoInbox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FinalFeatureGapClosure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailSequences",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TriggerType = table.Column<int>(type: "integer", nullable: false),
                    DelayMinutes = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailSequences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SequenceExecutions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    SequenceId = table.Column<string>(type: "text", nullable: false),
                    ConversationId = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    CurrentStep = table.Column<int>(type: "integer", nullable: false),
                    NextStepAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SequenceExecutions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SequenceSteps",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    SequenceId = table.Column<string>(type: "text", nullable: false),
                    StepOrder = table.Column<int>(type: "integer", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    HtmlBody = table.Column<string>(type: "text", nullable: false),
                    TextBody = table.Column<string>(type: "text", nullable: true),
                    DelayAfterPreviousMinutes = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SequenceSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SequenceSteps_EmailSequences_SequenceId",
                        column: x => x.SequenceId,
                        principalTable: "EmailSequences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SequenceExecutions_NextStepAt",
                table: "SequenceExecutions",
                column: "NextStepAt");

            migrationBuilder.CreateIndex(
                name: "IX_SequenceSteps_SequenceId",
                table: "SequenceSteps",
                column: "SequenceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SequenceExecutions");

            migrationBuilder.DropTable(
                name: "SequenceSteps");

            migrationBuilder.DropTable(
                name: "EmailSequences");
        }
    }
}
