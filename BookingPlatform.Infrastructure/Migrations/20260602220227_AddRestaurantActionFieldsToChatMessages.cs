using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantActionFieldsToChatMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActionCompleted",
                table: "chat_messages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "RestaurantOrderId",
                table: "chat_messages",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RestaurantTableReservationId",
                table: "chat_messages",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_RestaurantOrderId",
                table: "chat_messages",
                column: "RestaurantOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_RestaurantTableReservationId",
                table: "chat_messages",
                column: "RestaurantTableReservationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_chat_messages_RestaurantOrderId",
                table: "chat_messages");

            migrationBuilder.DropIndex(
                name: "IX_chat_messages_RestaurantTableReservationId",
                table: "chat_messages");

            migrationBuilder.DropColumn(
                name: "IsActionCompleted",
                table: "chat_messages");

            migrationBuilder.DropColumn(
                name: "RestaurantOrderId",
                table: "chat_messages");

            migrationBuilder.DropColumn(
                name: "RestaurantTableReservationId",
                table: "chat_messages");
        }
    }
}
