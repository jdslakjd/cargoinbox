using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CargoInbox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMailboxBindingGateway : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ImapUseSsl",
                table: "UserMailConfigs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ProviderType",
                table: "UserMailConfigs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SharedInboxId",
                table: "UserMailConfigs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SmtpUseSsl",
                table: "UserMailConfigs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "SharedInboxes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ImapUseSsl",
                table: "SharedInboxes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ProviderType",
                table: "SharedInboxes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "SmtpUseSsl",
                table: "SharedInboxes",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImapUseSsl",
                table: "UserMailConfigs");

            migrationBuilder.DropColumn(
                name: "ProviderType",
                table: "UserMailConfigs");

            migrationBuilder.DropColumn(
                name: "SharedInboxId",
                table: "UserMailConfigs");

            migrationBuilder.DropColumn(
                name: "SmtpUseSsl",
                table: "UserMailConfigs");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "SharedInboxes");

            migrationBuilder.DropColumn(
                name: "ImapUseSsl",
                table: "SharedInboxes");

            migrationBuilder.DropColumn(
                name: "ProviderType",
                table: "SharedInboxes");

            migrationBuilder.DropColumn(
                name: "SmtpUseSsl",
                table: "SharedInboxes");
        }
    }
}
