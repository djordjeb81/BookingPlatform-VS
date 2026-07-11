using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerProfileStructuredDeliveryAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultDeliveryApartment",
                table: "customer_profiles",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultDeliveryCity",
                table: "customer_profiles",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultDeliveryNote",
                table: "customer_profiles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultDeliveryStreet",
                table: "customer_profiles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultDeliveryStreetNumber",
                table: "customer_profiles",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultDeliveryApartment",
                table: "customer_profiles");

            migrationBuilder.DropColumn(
                name: "DefaultDeliveryCity",
                table: "customer_profiles");

            migrationBuilder.DropColumn(
                name: "DefaultDeliveryNote",
                table: "customer_profiles");

            migrationBuilder.DropColumn(
                name: "DefaultDeliveryStreet",
                table: "customer_profiles");

            migrationBuilder.DropColumn(
                name: "DefaultDeliveryStreetNumber",
                table: "customer_profiles");
        }
    }
}
