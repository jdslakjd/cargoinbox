using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CargoInbox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarAndMeetingsSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CalendarEvents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    StartTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OrganizerUserId = table.Column<string>(type: "text", nullable: false),
                    OrganizerUserName = table.Column<string>(type: "text", nullable: false),
                    RelatedContactId = table.Column<string>(type: "text", nullable: true),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    MeetingUrl = table.Column<string>(type: "text", nullable: true),
                    IsCancelled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_OrganizerUserId_StartTimeUtc",
                table: "CalendarEvents",
                columns: new[] { "OrganizerUserId", "StartTimeUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalendarEvents");
        }
    }
}
