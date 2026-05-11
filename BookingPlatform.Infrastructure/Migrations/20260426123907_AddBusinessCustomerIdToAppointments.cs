using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessCustomerIdToAppointments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "BusinessCustomerId",
                table: "appointments",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_appointments_BusinessCustomerId",
                table: "appointments",
                column: "BusinessCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_appointments_BusinessId_BusinessCustomerId_StartAtUtc",
                table: "appointments",
                columns: new[] { "BusinessId", "BusinessCustomerId", "StartAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_appointments_business_customers_BusinessCustomerId",
                table: "appointments",
                column: "BusinessCustomerId",
                principalTable: "business_customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_appointments_business_customers_BusinessCustomerId",
                table: "appointments");

            migrationBuilder.DropIndex(
                name: "IX_appointments_BusinessCustomerId",
                table: "appointments");

            migrationBuilder.DropIndex(
                name: "IX_appointments_BusinessId_BusinessCustomerId_StartAtUtc",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "BusinessCustomerId",
                table: "appointments");
        }
    }
}
