using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerLinksToRestaurantTableReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AppUserId",
                table: "restaurant_table_reservations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "BusinessCustomerId",
                table: "restaurant_table_reservations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CustomerProfileId",
                table: "restaurant_table_reservations",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_table_reservations_AppUserId",
                table: "restaurant_table_reservations",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_table_reservations_BusinessCustomerId",
                table: "restaurant_table_reservations",
                column: "BusinessCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_table_reservations_CustomerProfileId",
                table: "restaurant_table_reservations",
                column: "CustomerProfileId");

            migrationBuilder.AddForeignKey(
                name: "FK_restaurant_table_reservations_app_users_AppUserId",
                table: "restaurant_table_reservations",
                column: "AppUserId",
                principalTable: "app_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_restaurant_table_reservations_business_customers_BusinessCu~",
                table: "restaurant_table_reservations",
                column: "BusinessCustomerId",
                principalTable: "business_customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_restaurant_table_reservations_customer_profiles_CustomerPro~",
                table: "restaurant_table_reservations",
                column: "CustomerProfileId",
                principalTable: "customer_profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_restaurant_table_reservations_app_users_AppUserId",
                table: "restaurant_table_reservations");

            migrationBuilder.DropForeignKey(
                name: "FK_restaurant_table_reservations_business_customers_BusinessCu~",
                table: "restaurant_table_reservations");

            migrationBuilder.DropForeignKey(
                name: "FK_restaurant_table_reservations_customer_profiles_CustomerPro~",
                table: "restaurant_table_reservations");

            migrationBuilder.DropIndex(
                name: "IX_restaurant_table_reservations_AppUserId",
                table: "restaurant_table_reservations");

            migrationBuilder.DropIndex(
                name: "IX_restaurant_table_reservations_BusinessCustomerId",
                table: "restaurant_table_reservations");

            migrationBuilder.DropIndex(
                name: "IX_restaurant_table_reservations_CustomerProfileId",
                table: "restaurant_table_reservations");

            migrationBuilder.DropColumn(
                name: "AppUserId",
                table: "restaurant_table_reservations");

            migrationBuilder.DropColumn(
                name: "BusinessCustomerId",
                table: "restaurant_table_reservations");

            migrationBuilder.DropColumn(
                name: "CustomerProfileId",
                table: "restaurant_table_reservations");
        }
    }
}
