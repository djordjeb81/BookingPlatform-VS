using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantOrderItemAddons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "PriceDeltaSnapshot",
                table: "restaurant_order_item_options",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "OptionNameSnapshot",
                table: "restaurant_order_item_options",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<long>(
                name: "MenuItemOptionId",
                table: "restaurant_order_item_options",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<int>(
                name: "AmountMode",
                table: "restaurant_order_item_options",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "RestaurantAddonId",
                table: "restaurant_order_item_options",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_order_item_options_RestaurantAddonId",
                table: "restaurant_order_item_options",
                column: "RestaurantAddonId");

            migrationBuilder.AddForeignKey(
                name: "FK_restaurant_order_item_options_restaurant_addons_RestaurantA~",
                table: "restaurant_order_item_options",
                column: "RestaurantAddonId",
                principalTable: "restaurant_addons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_restaurant_order_item_options_restaurant_addons_RestaurantA~",
                table: "restaurant_order_item_options");

            migrationBuilder.DropIndex(
                name: "IX_restaurant_order_item_options_RestaurantAddonId",
                table: "restaurant_order_item_options");

            migrationBuilder.DropColumn(
                name: "AmountMode",
                table: "restaurant_order_item_options");

            migrationBuilder.DropColumn(
                name: "RestaurantAddonId",
                table: "restaurant_order_item_options");

            migrationBuilder.AlterColumn<decimal>(
                name: "PriceDeltaSnapshot",
                table: "restaurant_order_item_options",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "OptionNameSnapshot",
                table: "restaurant_order_item_options",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<long>(
                name: "MenuItemOptionId",
                table: "restaurant_order_item_options",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }
    }
}
