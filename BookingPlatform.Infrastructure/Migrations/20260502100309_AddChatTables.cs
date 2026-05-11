using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChatTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "chat_conversations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BusinessId = table.Column<long>(type: "bigint", nullable: false),
                    BusinessCustomerId = table.Column<long>(type: "bigint", nullable: true),
                    CustomerProfileId = table.Column<long>(type: "bigint", nullable: true),
                    AppUserId = table.Column<long>(type: "bigint", nullable: true),
                    LastMessageAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastMessageText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UnreadForBusinessCount = table.Column<int>(type: "integer", nullable: false),
                    UnreadForCustomerCount = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_conversations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "chat_messages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConversationId = table.Column<long>(type: "bigint", nullable: false),
                    SenderType = table.Column<int>(type: "integer", nullable: false),
                    SenderUserId = table.Column<long>(type: "bigint", nullable: true),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ReadByBusinessAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReadByCustomerAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_messages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_chat_conversations_BusinessCustomerId",
                table: "chat_conversations",
                column: "BusinessCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_chat_conversations_BusinessId",
                table: "chat_conversations",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_chat_conversations_BusinessId_BusinessCustomerId",
                table: "chat_conversations",
                columns: new[] { "BusinessId", "BusinessCustomerId" },
                unique: true,
                filter: "\"BusinessCustomerId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_chat_conversations_LastMessageAtUtc",
                table: "chat_conversations",
                column: "LastMessageAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_ConversationId",
                table: "chat_messages",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_ConversationId_CreatedAtUtc",
                table: "chat_messages",
                columns: new[] { "ConversationId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chat_conversations");

            migrationBuilder.DropTable(
                name: "chat_messages");
        }
    }
}
