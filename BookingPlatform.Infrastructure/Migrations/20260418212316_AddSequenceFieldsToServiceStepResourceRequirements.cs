using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSequenceFieldsToServiceStepResourceRequirements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SequenceOrder",
                table: "ServiceStepResourceRequirements",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UsageDurationMin",
                table: "ServiceStepResourceRequirements",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SequenceOrder",
                table: "ServiceStepResourceRequirements");

            migrationBuilder.DropColumn(
                name: "UsageDurationMin",
                table: "ServiceStepResourceRequirements");
        }
    }
}
