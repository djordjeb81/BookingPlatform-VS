using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReservedResourceFieldsToAppointments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PartySize",
                table: "appointments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReleasedAtUtc",
                table: "appointments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ReservedResourceId",
                table: "appointments",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_appointments_BusinessId_ReservedResourceId_StartAtUtc",
                table: "appointments",
                columns: new[] { "BusinessId", "ReservedResourceId", "StartAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_appointments_ReservedResourceId",
                table: "appointments",
                column: "ReservedResourceId");

            migrationBuilder.AddForeignKey(
                name: "FK_appointments_resources_ReservedResourceId",
                table: "appointments",
                column: "ReservedResourceId",
                principalTable: "resources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_appointments_resources_ReservedResourceId",
                table: "appointments");

            migrationBuilder.DropIndex(
                name: "IX_appointments_BusinessId_ReservedResourceId_StartAtUtc",
                table: "appointments");

            migrationBuilder.DropIndex(
                name: "IX_appointments_ReservedResourceId",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "PartySize",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "ReleasedAtUtc",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "ReservedResourceId",
                table: "appointments");
        }
    }
}
