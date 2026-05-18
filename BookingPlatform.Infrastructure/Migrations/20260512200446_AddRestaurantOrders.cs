using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "restaurant_orders",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BusinessId = table.Column<long>(type: "bigint", nullable: false),
                    RestaurantAreaId = table.Column<long>(type: "bigint", nullable: true),
                    TableResourceId = table.Column<long>(type: "bigint", nullable: true),
                    TableSessionId = table.Column<long>(type: "bigint", nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CustomerPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SubtotalAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "RSD"),
                    SubmittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restaurant_orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_restaurant_orders_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_restaurant_orders_restaurant_areas_RestaurantAreaId",
                        column: x => x.RestaurantAreaId,
                        principalTable: "restaurant_areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_restaurant_orders_restaurant_table_sessions_TableSessionId",
                        column: x => x.TableSessionId,
                        principalTable: "restaurant_table_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "restaurant_order_items",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    MenuItemId = table.Column<long>(type: "bigint", nullable: false),
                    MenuItemNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UnitPriceSnapshot = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    LineSubtotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restaurant_order_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_restaurant_order_items_restaurant_menu_items_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "restaurant_menu_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_restaurant_order_items_restaurant_orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "restaurant_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "restaurant_order_item_options",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderItemId = table.Column<long>(type: "bigint", nullable: false),
                    MenuItemOptionId = table.Column<long>(type: "bigint", nullable: false),
                    OptionNameSnapshot = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    PriceDeltaSnapshot = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restaurant_order_item_options", x => x.Id);
                    table.ForeignKey(
                        name: "FK_restaurant_order_item_options_restaurant_menu_item_options_~",
                        column: x => x.MenuItemOptionId,
                        principalTable: "restaurant_menu_item_options",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_restaurant_order_item_options_restaurant_order_items_OrderI~",
                        column: x => x.OrderItemId,
                        principalTable: "restaurant_order_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_order_item_options_MenuItemOptionId",
                table: "restaurant_order_item_options",
                column: "MenuItemOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_order_item_options_OrderItemId",
                table: "restaurant_order_item_options",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_order_items_MenuItemId",
                table: "restaurant_order_items",
                column: "MenuItemId");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_order_items_OrderId",
                table: "restaurant_order_items",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_orders_BusinessId",
                table: "restaurant_orders",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_orders_RestaurantAreaId",
                table: "restaurant_orders",
                column: "RestaurantAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_orders_Status",
                table: "restaurant_orders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_orders_TableResourceId",
                table: "restaurant_orders",
                column: "TableResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_orders_TableSessionId",
                table: "restaurant_orders",
                column: "TableSessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "restaurant_order_item_options");

            migrationBuilder.DropTable(
                name: "restaurant_order_items");

            migrationBuilder.DropTable(
                name: "restaurant_orders");
        }
    }
}
