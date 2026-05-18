using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantOrderMessageRecipients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SenderOperationUnitId",
                table: "restaurant_order_messages",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "restaurant_order_message_recipients",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BusinessId = table.Column<long>(type: "bigint", nullable: false),
                    MessageId = table.Column<long>(type: "bigint", nullable: false),
                    RecipientType = table.Column<int>(type: "integer", nullable: false),
                    RecipientOperationUnitId = table.Column<long>(type: "bigint", nullable: true),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ReadAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restaurant_order_message_recipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_restaurant_order_message_recipients_restaurant_order_messag~",
                        column: x => x.MessageId,
                        principalTable: "restaurant_order_messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_order_messages_BusinessId_SenderOperationUnitId_~",
                table: "restaurant_order_messages",
                columns: new[] { "BusinessId", "SenderOperationUnitId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_order_messages_SenderOperationUnitId",
                table: "restaurant_order_messages",
                column: "SenderOperationUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_order_message_recipients_BusinessId",
                table: "restaurant_order_message_recipients",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_order_message_recipients_BusinessId_RecipientOpe~",
                table: "restaurant_order_message_recipients",
                columns: new[] { "BusinessId", "RecipientOperationUnitId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_order_message_recipients_BusinessId_RecipientTyp~",
                table: "restaurant_order_message_recipients",
                columns: new[] { "BusinessId", "RecipientType", "RecipientOperationUnitId", "MessageId" });

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_order_message_recipients_MessageId",
                table: "restaurant_order_message_recipients",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_order_message_recipients_RecipientOperationUnitId",
                table: "restaurant_order_message_recipients",
                column: "RecipientOperationUnitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "restaurant_order_message_recipients");

            migrationBuilder.DropIndex(
                name: "IX_restaurant_order_messages_BusinessId_SenderOperationUnitId_~",
                table: "restaurant_order_messages");

            migrationBuilder.DropIndex(
                name: "IX_restaurant_order_messages_SenderOperationUnitId",
                table: "restaurant_order_messages");

            migrationBuilder.DropColumn(
                name: "SenderOperationUnitId",
                table: "restaurant_order_messages");
        }
    }
}
