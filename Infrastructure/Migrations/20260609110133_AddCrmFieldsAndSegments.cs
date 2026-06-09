using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CargoInbox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCrmFieldsAndSegments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CrmFieldDefinitions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    EntityType = table.Column<int>(type: "integer", nullable: false),
                    FieldKey = table.Column<string>(type: "text", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    FieldType = table.Column<int>(type: "integer", nullable: false),
                    OptionsJson = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmFieldDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmSegments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    FilterJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmSegments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmFieldValues",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    FieldDefinitionId = table.Column<string>(type: "text", nullable: false),
                    EntityId = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmFieldValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CrmFieldValues_CrmFieldDefinitions_FieldDefinitionId",
                        column: x => x.FieldDefinitionId,
                        principalTable: "CrmFieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: "default",
                column: "CreatedAt",
                value: new DateTime(2026, 6, 9, 11, 1, 33, 375, DateTimeKind.Utc).AddTicks(5810));

            migrationBuilder.CreateIndex(
                name: "IX_CrmFieldDefinitions_TenantId_EntityType_FieldKey",
                table: "CrmFieldDefinitions",
                columns: new[] { "TenantId", "EntityType", "FieldKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrmFieldValues_FieldDefinitionId_EntityId",
                table: "CrmFieldValues",
                columns: new[] { "FieldDefinitionId", "EntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrmSegments_TenantId",
                table: "CrmSegments",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CrmFieldValues");

            migrationBuilder.DropTable(
                name: "CrmSegments");

            migrationBuilder.DropTable(
                name: "CrmFieldDefinitions");

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: "default",
                column: "CreatedAt",
                value: new DateTime(2026, 6, 9, 10, 54, 53, 20, DateTimeKind.Utc).AddTicks(5962));
        }
    }
}
