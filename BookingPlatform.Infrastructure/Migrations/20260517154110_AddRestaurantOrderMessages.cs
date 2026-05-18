using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantOrderMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "restaurant_order_messages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BusinessId = table.Column<long>(type: "bigint", nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    SenderType = table.Column<int>(type: "integer", nullable: false),
                    MessageType = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ActionKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActionRequired = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsActionCompleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ActionCompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restaurant_order_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_restaurant_order_messages_restaurant_orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "restaurant_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_order_messages_BusinessId",
                table: "restaurant_order_messages",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_order_messages_BusinessId_IsActionRequired_IsAct~",
                table: "restaurant_order_messages",
                columns: new[] { "BusinessId", "IsActionRequired", "IsActionCompleted" });

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_order_messages_OrderId",
                table: "restaurant_order_messages",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_order_messages_OrderId_CreatedAtUtc",
                table: "restaurant_order_messages",
                columns: new[] { "OrderId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "restaurant_order_messages");
        }
    }
}
