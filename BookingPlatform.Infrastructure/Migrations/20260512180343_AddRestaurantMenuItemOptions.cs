using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantMenuItemOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "restaurant_menu_item_option_groups",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MenuItemId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    MinSelected = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    MaxSelected = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restaurant_menu_item_option_groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_restaurant_menu_item_option_groups_restaurant_menu_items_Me~",
                        column: x => x.MenuItemId,
                        principalTable: "restaurant_menu_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "restaurant_menu_item_options",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OptionGroupId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    PriceDelta = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restaurant_menu_item_options", x => x.Id);
                    table.ForeignKey(
                        name: "FK_restaurant_menu_item_options_restaurant_menu_item_option_gr~",
                        column: x => x.OptionGroupId,
                        principalTable: "restaurant_menu_item_option_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_menu_item_option_groups_MenuItemId",
                table: "restaurant_menu_item_option_groups",
                column: "MenuItemId");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_menu_item_option_groups_MenuItemId_Name",
                table: "restaurant_menu_item_option_groups",
                columns: new[] { "MenuItemId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_menu_item_options_OptionGroupId",
                table: "restaurant_menu_item_options",
                column: "OptionGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_menu_item_options_OptionGroupId_Name",
                table: "restaurant_menu_item_options",
                columns: new[] { "OptionGroupId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "restaurant_menu_item_options");

            migrationBuilder.DropTable(
                name: "restaurant_menu_item_option_groups");
        }
    }
}
