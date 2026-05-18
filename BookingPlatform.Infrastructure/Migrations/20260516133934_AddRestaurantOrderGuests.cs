using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantOrderGuests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "OrderGuestId",
                table: "restaurant_order_items",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "restaurant_order_guests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restaurant_order_guests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_restaurant_order_guests_restaurant_orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "restaurant_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_order_items_OrderGuestId",
                table: "restaurant_order_items",
                column: "OrderGuestId");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_order_guests_OrderId",
                table: "restaurant_order_guests",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_order_guests_OrderId_DisplayOrder",
                table: "restaurant_order_guests",
                columns: new[] { "OrderId", "DisplayOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_restaurant_order_items_restaurant_order_guests_OrderGuestId",
                table: "restaurant_order_items",
                column: "OrderGuestId",
                principalTable: "restaurant_order_guests",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_restaurant_order_items_restaurant_order_guests_OrderGuestId",
                table: "restaurant_order_items");

            migrationBuilder.DropTable(
                name: "restaurant_order_guests");

            migrationBuilder.DropIndex(
                name: "IX_restaurant_order_items_OrderGuestId",
                table: "restaurant_order_items");

            migrationBuilder.DropColumn(
                name: "OrderGuestId",
                table: "restaurant_order_items");
        }
    }
}
