using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedRestaurantOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SharedRestaurantOrderId",
                table: "chat_messages",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "shared_restaurant_orders",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerCustomerProfileId = table.Column<long>(type: "bigint", nullable: false),
                    OwnerAppUserId = table.Column<long>(type: "bigint", nullable: true),
                    OwnerDisplayNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SentToChatAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shared_restaurant_orders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "shared_restaurant_order_items",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SharedRestaurantOrderId = table.Column<long>(type: "bigint", nullable: false),
                    BusinessId = table.Column<long>(type: "bigint", nullable: false),
                    BusinessNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AddedByCustomerProfileId = table.Column<long>(type: "bigint", nullable: false),
                    AddedByAppUserId = table.Column<long>(type: "bigint", nullable: true),
                    AddedByDisplayNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OrderPersonName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MenuItemId = table.Column<long>(type: "bigint", nullable: false),
                    MenuItemNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UnitPriceSnapshot = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    LineSubtotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    SendToKitchenSnapshot = table.Column<bool>(type: "boolean", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SourceSharedRestaurantOrderId = table.Column<long>(type: "bigint", nullable: true),
                    SourceSharedRestaurantOrderItemId = table.Column<long>(type: "bigint", nullable: true),
                    SourceChatMessageId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shared_restaurant_order_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shared_restaurant_order_items_shared_restaurant_orders_Shar~",
                        column: x => x.SharedRestaurantOrderId,
                        principalTable: "shared_restaurant_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shared_restaurant_order_item_options",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SharedRestaurantOrderItemId = table.Column<long>(type: "bigint", nullable: false),
                    RestaurantAddonId = table.Column<long>(type: "bigint", nullable: true),
                    OptionNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PriceDeltaSnapshot = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    AmountMode = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shared_restaurant_order_item_options", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shared_restaurant_order_item_options_shared_restaurant_orde~",
                        column: x => x.SharedRestaurantOrderItemId,
                        principalTable: "shared_restaurant_order_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_SharedRestaurantOrderId",
                table: "chat_messages",
                column: "SharedRestaurantOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_chat_conversation_members_ConversationId",
                table: "chat_conversation_members",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_shared_restaurant_order_item_options_RestaurantAddonId",
                table: "shared_restaurant_order_item_options",
                column: "RestaurantAddonId");

            migrationBuilder.CreateIndex(
                name: "IX_shared_restaurant_order_item_options_SharedRestaurantOrderI~",
                table: "shared_restaurant_order_item_options",
                column: "SharedRestaurantOrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_shared_restaurant_order_items_AddedByCustomerProfileId",
                table: "shared_restaurant_order_items",
                column: "AddedByCustomerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_shared_restaurant_order_items_BusinessId",
                table: "shared_restaurant_order_items",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_shared_restaurant_order_items_MenuItemId",
                table: "shared_restaurant_order_items",
                column: "MenuItemId");

            migrationBuilder.CreateIndex(
                name: "IX_shared_restaurant_order_items_SharedRestaurantOrderId",
                table: "shared_restaurant_order_items",
                column: "SharedRestaurantOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_shared_restaurant_orders_OwnerAppUserId",
                table: "shared_restaurant_orders",
                column: "OwnerAppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_shared_restaurant_orders_OwnerCustomerProfileId",
                table: "shared_restaurant_orders",
                column: "OwnerCustomerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_shared_restaurant_orders_OwnerCustomerProfileId_Status",
                table: "shared_restaurant_orders",
                columns: new[] { "OwnerCustomerProfileId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_shared_restaurant_orders_Status",
                table: "shared_restaurant_orders",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shared_restaurant_order_item_options");

            migrationBuilder.DropTable(
                name: "shared_restaurant_order_items");

            migrationBuilder.DropTable(
                name: "shared_restaurant_orders");

            migrationBuilder.DropIndex(
                name: "IX_chat_messages_SharedRestaurantOrderId",
                table: "chat_messages");

            migrationBuilder.DropIndex(
                name: "IX_chat_conversation_members_ConversationId",
                table: "chat_conversation_members");

            migrationBuilder.DropColumn(
                name: "SharedRestaurantOrderId",
                table: "chat_messages");
        }
    }
}
