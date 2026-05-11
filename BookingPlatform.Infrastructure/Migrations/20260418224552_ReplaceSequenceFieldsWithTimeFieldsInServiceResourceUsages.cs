using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceSequenceFieldsWithTimeFieldsInServiceResourceUsages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ServiceResourceUsages_ServiceId_SequenceOrder",
                table: "ServiceResourceUsages");

            migrationBuilder.RenameColumn(
                name: "UsageDurationMin",
                table: "ServiceResourceUsages",
                newName: "StartMinute");

            migrationBuilder.RenameColumn(
                name: "SequenceOrder",
                table: "ServiceResourceUsages",
                newName: "DurationMin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "StartMinute",
                table: "ServiceResourceUsages",
                newName: "UsageDurationMin");

            migrationBuilder.RenameColumn(
                name: "DurationMin",
                table: "ServiceResourceUsages",
                newName: "SequenceOrder");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceResourceUsages_ServiceId_SequenceOrder",
                table: "ServiceResourceUsages",
                columns: new[] { "ServiceId", "SequenceOrder" },
                unique: true);
        }
    }
}
