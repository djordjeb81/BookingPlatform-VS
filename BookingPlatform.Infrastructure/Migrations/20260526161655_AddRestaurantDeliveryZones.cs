using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantDeliveryZones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryFeeAmount",
                table: "restaurant_orders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryMinimumOrderAmountSnapshot",
                table: "restaurant_orders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<long>(
                name: "DeliveryZoneId",
                table: "restaurant_orders",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryZoneNameSnapshot",
                table: "restaurant_orders",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "restaurant_delivery_zones",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BusinessId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DeliveryFeeAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MinimumOrderAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restaurant_delivery_zones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_restaurant_delivery_zones_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_delivery_zones_BusinessId",
                table: "restaurant_delivery_zones",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_delivery_zones_BusinessId_DisplayOrder",
                table: "restaurant_delivery_zones",
                columns: new[] { "BusinessId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_delivery_zones_BusinessId_Name",
                table: "restaurant_delivery_zones",
                columns: new[] { "BusinessId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "restaurant_delivery_zones");

            migrationBuilder.DropColumn(
                name: "DeliveryFeeAmount",
                table: "restaurant_orders");

            migrationBuilder.DropColumn(
                name: "DeliveryMinimumOrderAmountSnapshot",
                table: "restaurant_orders");

            migrationBuilder.DropColumn(
                name: "DeliveryZoneId",
                table: "restaurant_orders");

            migrationBuilder.DropColumn(
                name: "DeliveryZoneNameSnapshot",
                table: "restaurant_orders");
        }
    }
}
