using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerProfileDisplayAndDeliveryDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                table: "customer_profiles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultDeliveryAddress",
                table: "customer_profiles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DefaultDeliveryLatitude",
                table: "customer_profiles",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DefaultDeliveryLongitude",
                table: "customer_profiles",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Nickname",
                table: "customer_profiles",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                table: "customer_profiles");

            migrationBuilder.DropColumn(
                name: "DefaultDeliveryAddress",
                table: "customer_profiles");

            migrationBuilder.DropColumn(
                name: "DefaultDeliveryLatitude",
                table: "customer_profiles");

            migrationBuilder.DropColumn(
                name: "DefaultDeliveryLongitude",
                table: "customer_profiles");

            migrationBuilder.DropColumn(
                name: "Nickname",
                table: "customer_profiles");
        }
    }
}
