using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantOrderTypeAndDeliveryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveryAddress",
                table: "restaurant_orders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryNote",
                table: "restaurant_orders",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrderType",
                table: "restaurant_orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "RequestedPickupAtUtc",
                table: "restaurant_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_orders_OrderType",
                table: "restaurant_orders",
                column: "OrderType");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_orders_RequestedPickupAtUtc",
                table: "restaurant_orders",
                column: "RequestedPickupAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_restaurant_orders_OrderType",
                table: "restaurant_orders");

            migrationBuilder.DropIndex(
                name: "IX_restaurant_orders_RequestedPickupAtUtc",
                table: "restaurant_orders");

            migrationBuilder.DropColumn(
                name: "DeliveryAddress",
                table: "restaurant_orders");

            migrationBuilder.DropColumn(
                name: "DeliveryNote",
                table: "restaurant_orders");

            migrationBuilder.DropColumn(
                name: "OrderType",
                table: "restaurant_orders");

            migrationBuilder.DropColumn(
                name: "RequestedPickupAtUtc",
                table: "restaurant_orders");
        }
    }
}
