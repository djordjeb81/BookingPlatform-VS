using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantOrderDailyNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DailyOrderNumber",
                table: "restaurant_orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "OrderDateLocal",
                table: "restaurant_orders",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.Sql("""
                WITH numbered AS (
                    SELECT
                        "Id",
                        ("CreatedAtUtc" AT TIME ZONE 'Europe/Belgrade')::date AS local_date,
                        ROW_NUMBER() OVER (
                            PARTITION BY "BusinessId", ("CreatedAtUtc" AT TIME ZONE 'Europe/Belgrade')::date
                            ORDER BY "CreatedAtUtc", "Id"
                        ) AS rn
                    FROM restaurant_orders
                )
                UPDATE restaurant_orders AS ro
                SET
                    "OrderDateLocal" = numbered.local_date,
                    "DailyOrderNumber" = numbered.rn
                FROM numbered
                WHERE ro."Id" = numbered."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_orders_BusinessId_OrderDateLocal",
                table: "restaurant_orders",
                columns: new[] { "BusinessId", "OrderDateLocal" });

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_orders_BusinessId_OrderDateLocal_DailyOrderNumber",
                table: "restaurant_orders",
                columns: new[] { "BusinessId", "OrderDateLocal", "DailyOrderNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_restaurant_orders_BusinessId_OrderDateLocal",
                table: "restaurant_orders");

            migrationBuilder.DropIndex(
                name: "IX_restaurant_orders_BusinessId_OrderDateLocal_DailyOrderNumber",
                table: "restaurant_orders");

            migrationBuilder.DropColumn(
                name: "DailyOrderNumber",
                table: "restaurant_orders");

            migrationBuilder.DropColumn(
                name: "OrderDateLocal",
                table: "restaurant_orders");
        }
    }
}