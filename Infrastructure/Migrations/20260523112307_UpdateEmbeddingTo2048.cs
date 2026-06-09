using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace CargoInbox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateEmbeddingTo2048 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop HNSW indexes (max 2000 dims) before altering to vector(2048)
            migrationBuilder.DropIndex(
                name: "IX_Mails_Embedding",
                table: "Mails");

            migrationBuilder.DropIndex(
                name: "IX_ConversationMessages_Embedding",
                table: "ConversationMessages");

            migrationBuilder.AlterColumn<Vector>(
                name: "Embedding",
                table: "Mails",
                type: "vector(2048)",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector(1536)",
                oldNullable: true);

            migrationBuilder.AlterColumn<Vector>(
                name: "Embedding",
                table: "ConversationMessages",
                type: "vector(2048)",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector(1536)",
                oldNullable: true);

            // Note: No vector index recreated — pgvector caps indexes at 2000 dims,
            // model uses 2048. Similarity searches will use exact (full scan).

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: "default",
                column: "CreatedAt",
                value: new DateTime(2026, 5, 23, 11, 23, 6, 576, DateTimeKind.Utc).AddTicks(5620));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Vector>(
                name: "Embedding",
                table: "Mails",
                type: "vector(1536)",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector(2048)",
                oldNullable: true);

            migrationBuilder.AlterColumn<Vector>(
                name: "Embedding",
                table: "ConversationMessages",
                type: "vector(1536)",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector(2048)",
                oldNullable: true);

            // Restore original HNSW indexes (safe at 1536 dims)
            migrationBuilder.CreateIndex(
                name: "IX_Mails_Embedding",
                table: "Mails",
                column: "Embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_Embedding",
                table: "ConversationMessages",
                column: "Embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: "default",
                column: "CreatedAt",
                value: new DateTime(2026, 5, 23, 6, 52, 21, 50, DateTimeKind.Utc).AddTicks(603));
        }
    }
}
