using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChatMessageActionMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActionType",
                table: "chat_messages",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AppointmentId",
                table: "chat_messages",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ChangeRequestId",
                table: "chat_messages",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_AppointmentId",
                table: "chat_messages",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_ChangeRequestId",
                table: "chat_messages",
                column: "ChangeRequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_chat_messages_AppointmentId",
                table: "chat_messages");

            migrationBuilder.DropIndex(
                name: "IX_chat_messages_ChangeRequestId",
                table: "chat_messages");

            migrationBuilder.DropColumn(
                name: "ActionType",
                table: "chat_messages");

            migrationBuilder.DropColumn(
                name: "AppointmentId",
                table: "chat_messages");

            migrationBuilder.DropColumn(
                name: "ChangeRequestId",
                table: "chat_messages");
        }
    }
}
