using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueBusinessCustomerProfilePerBusiness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_business_customers_BusinessId_CustomerProfileId",
                table: "business_customers");

            migrationBuilder.CreateIndex(
                name: "IX_business_customers_BusinessId_CustomerProfileId",
                table: "business_customers",
                columns: new[] { "BusinessId", "CustomerProfileId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_business_customers_BusinessId_CustomerProfileId",
                table: "business_customers");

            migrationBuilder.CreateIndex(
                name: "IX_business_customers_BusinessId_CustomerProfileId",
                table: "business_customers",
                columns: new[] { "BusinessId", "CustomerProfileId" });
        }
    }
}
