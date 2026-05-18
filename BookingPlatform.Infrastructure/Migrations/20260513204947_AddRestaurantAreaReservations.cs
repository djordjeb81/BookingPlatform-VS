using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantAreaReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "restaurant_area_reservations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BusinessId = table.Column<long>(type: "bigint", nullable: false),
                    RestaurantAreaId = table.Column<long>(type: "bigint", nullable: false),
                    PartySize = table.Column<int>(type: "integer", nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CustomerPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CustomerEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReservationAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpectedDurationMin = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    InternalNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RespondedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArrivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restaurant_area_reservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_restaurant_area_reservations_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_restaurant_area_reservations_restaurant_areas_RestaurantAre~",
                        column: x => x.RestaurantAreaId,
                        principalTable: "restaurant_areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_area_reservations_BusinessId",
                table: "restaurant_area_reservations",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_area_reservations_BusinessId_RestaurantAreaId_Re~",
                table: "restaurant_area_reservations",
                columns: new[] { "BusinessId", "RestaurantAreaId", "ReservationAtUtc", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_area_reservations_ReservationAtUtc",
                table: "restaurant_area_reservations",
                column: "ReservationAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_area_reservations_RestaurantAreaId",
                table: "restaurant_area_reservations",
                column: "RestaurantAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_area_reservations_Status",
                table: "restaurant_area_reservations",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "restaurant_area_reservations");
        }
    }
}
