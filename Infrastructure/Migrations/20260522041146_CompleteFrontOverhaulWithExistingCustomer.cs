using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CargoInbox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompleteFrontOverhaulWithExistingCustomer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MailComments_Conversations_ConversationId1",
                table: "MailComments");

            migrationBuilder.DropIndex(
                name: "IX_MailComments_ConversationId1",
                table: "MailComments");

            migrationBuilder.DropColumn(
                name: "ConversationId1",
                table: "MailComments");

            migrationBuilder.AddColumn<string>(
                name: "CustomerId",
                table: "Conversations",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_CustomerId",
                table: "Conversations",
                column: "CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Customers_CustomerId",
                table: "Conversations",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Customers_CustomerId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_CustomerId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "Conversations");

            migrationBuilder.AddColumn<string>(
                name: "ConversationId1",
                table: "MailComments",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MailComments_ConversationId1",
                table: "MailComments",
                column: "ConversationId1");

            migrationBuilder.AddForeignKey(
                name: "FK_MailComments_Conversations_ConversationId1",
                table: "MailComments",
                column: "ConversationId1",
                principalTable: "Conversations",
                principalColumn: "Id");
        }
    }
}
