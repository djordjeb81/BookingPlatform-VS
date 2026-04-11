using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentChangeRequestExpireIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_appointment_change_requests_AppointmentId_Status_CreatedAtU~",
                table: "appointment_change_requests",
                columns: new[] { "AppointmentId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_appointment_change_requests_Status_ExpiresAtUtc",
                table: "appointment_change_requests",
                columns: new[] { "Status", "ExpiresAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_appointment_change_requests_AppointmentId_Status_CreatedAtU~",
                table: "appointment_change_requests");

            migrationBuilder.DropIndex(
                name: "IX_appointment_change_requests_Status_ExpiresAtUtc",
                table: "appointment_change_requests");
        }
    }
}
