using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVoidFieldsToFitnessMemberTrainingPasses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsVoided",
                table: "fitness_member_training_passes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                table: "fitness_member_training_passes",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VoidedAtUtc",
                table: "fitness_member_training_passes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "VoidedByUserId",
                table: "fitness_member_training_passes",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_fitness_member_training_passes_IsVoided",
                table: "fitness_member_training_passes",
                column: "IsVoided");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_fitness_member_training_passes_IsVoided",
                table: "fitness_member_training_passes");

            migrationBuilder.DropColumn(
                name: "IsVoided",
                table: "fitness_member_training_passes");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                table: "fitness_member_training_passes");

            migrationBuilder.DropColumn(
                name: "VoidedAtUtc",
                table: "fitness_member_training_passes");

            migrationBuilder.DropColumn(
                name: "VoidedByUserId",
                table: "fitness_member_training_passes");
        }
    }
}
