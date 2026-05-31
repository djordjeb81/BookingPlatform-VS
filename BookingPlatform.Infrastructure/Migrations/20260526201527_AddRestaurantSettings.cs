using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "restaurant_settings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BusinessId = table.Column<long>(type: "bigint", nullable: false),
                    PreparationReminderBufferMin = table.Column<int>(type: "integer", nullable: false, defaultValue: 10),
                    ScheduledOrderMinLeadTimeMin = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                    ScheduledOrderMaxDaysAhead = table.Column<int>(type: "integer", nullable: false, defaultValue: 7),
                    IsScheduledOrderingEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsDeliveryEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsDeliveryLocationRequired = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restaurant_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_restaurant_settings_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_settings_BusinessId",
                table: "restaurant_settings",
                column: "BusinessId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "restaurant_settings");
        }
    }
}
