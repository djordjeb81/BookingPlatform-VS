using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeRestaurantOperationUnitsFlexible : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_restaurant_operation_units_BusinessId_UnitType",
                table: "restaurant_operation_units");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_operation_units_BusinessId_Name",
                table: "restaurant_operation_units",
                columns: new[] { "BusinessId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_operation_units_BusinessId_UnitType",
                table: "restaurant_operation_units",
                columns: new[] { "BusinessId", "UnitType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_restaurant_operation_units_BusinessId_Name",
                table: "restaurant_operation_units");

            migrationBuilder.DropIndex(
                name: "IX_restaurant_operation_units_BusinessId_UnitType",
                table: "restaurant_operation_units");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_operation_units_BusinessId_UnitType",
                table: "restaurant_operation_units",
                columns: new[] { "BusinessId", "UnitType" },
                unique: true);
        }
    }
}
