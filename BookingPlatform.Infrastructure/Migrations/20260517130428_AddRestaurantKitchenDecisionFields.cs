using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantKitchenDecisionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "KitchenAcceptLaterMinutes",
                table: "restaurant_orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "KitchenAcceptedAtUtc",
                table: "restaurant_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KitchenDecisionStatus",
                table: "restaurant_orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "KitchenRejectNote",
                table: "restaurant_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KitchenRejectReason",
                table: "restaurant_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "KitchenRejectedAtUtc",
                table: "restaurant_orders",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KitchenAcceptLaterMinutes",
                table: "restaurant_orders");

            migrationBuilder.DropColumn(
                name: "KitchenAcceptedAtUtc",
                table: "restaurant_orders");

            migrationBuilder.DropColumn(
                name: "KitchenDecisionStatus",
                table: "restaurant_orders");

            migrationBuilder.DropColumn(
                name: "KitchenRejectNote",
                table: "restaurant_orders");

            migrationBuilder.DropColumn(
                name: "KitchenRejectReason",
                table: "restaurant_orders");

            migrationBuilder.DropColumn(
                name: "KitchenRejectedAtUtc",
                table: "restaurant_orders");
        }
    }
}
