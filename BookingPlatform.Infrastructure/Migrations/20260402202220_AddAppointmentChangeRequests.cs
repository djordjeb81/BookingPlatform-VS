using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentChangeRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "appointment_change_requests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AppointmentId = table.Column<long>(type: "bigint", nullable: false),
                    RequestType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    InitiatedBy = table.Column<int>(type: "integer", nullable: false),
                    OriginalStartAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OriginalEndAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProposedStartAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProposedEndAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RespondedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appointment_change_requests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_appointment_change_requests_AppointmentId",
                table: "appointment_change_requests",
                column: "AppointmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "appointment_change_requests");
        }
    }
}
