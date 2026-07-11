using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFitnessMemberIdToFitnessSessionBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "FitnessMemberId",
                table: "fitness_session_bookings",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_fitness_session_bookings_FitnessMemberId",
                table: "fitness_session_bookings",
                column: "FitnessMemberId");

            migrationBuilder.AddForeignKey(
                name: "FK_fitness_session_bookings_fitness_members_FitnessMemberId",
                table: "fitness_session_bookings",
                column: "FitnessMemberId",
                principalTable: "fitness_members",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_fitness_session_bookings_fitness_members_FitnessMemberId",
                table: "fitness_session_bookings");

            migrationBuilder.DropIndex(
                name: "IX_fitness_session_bookings_FitnessMemberId",
                table: "fitness_session_bookings");

            migrationBuilder.DropColumn(
                name: "FitnessMemberId",
                table: "fitness_session_bookings");
        }
    }
}
