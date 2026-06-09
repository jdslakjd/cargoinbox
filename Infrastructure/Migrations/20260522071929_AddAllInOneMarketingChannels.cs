using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CargoInbox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAllInOneMarketingChannels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FacebookPsid",
                table: "Contacts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeadSource",
                table: "Contacts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TikTokLeadId",
                table: "Contacts",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FacebookPsid",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "LeadSource",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "TikTokLeadId",
                table: "Contacts");
        }
    }
}
