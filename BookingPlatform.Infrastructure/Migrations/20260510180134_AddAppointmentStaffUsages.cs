using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentStaffUsages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "appointment_staff_usages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AppointmentId = table.Column<long>(type: "bigint", nullable: false),
                    StaffMemberId = table.Column<long>(type: "bigint", nullable: false),
                    StartMinute = table.Column<int>(type: "integer", nullable: false),
                    DurationMin = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appointment_staff_usages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_appointment_staff_usages_appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_appointment_staff_usages_staff_members_StaffMemberId",
                        column: x => x.StaffMemberId,
                        principalTable: "staff_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_appointment_staff_usages_AppointmentId",
                table: "appointment_staff_usages",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_appointment_staff_usages_StaffMemberId",
                table: "appointment_staff_usages",
                column: "StaffMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_appointment_staff_usages_StaffMemberId_AppointmentId",
                table: "appointment_staff_usages",
                columns: new[] { "StaffMemberId", "AppointmentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "appointment_staff_usages");
        }
    }
}
