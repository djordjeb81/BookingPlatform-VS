using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantOperationUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "restaurant_operation_units",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BusinessId = table.Column<long>(type: "bigint", nullable: false),
                    UnitType = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restaurant_operation_units", x => x.Id);
                    table.ForeignKey(
                        name: "FK_restaurant_operation_units_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "restaurant_operation_unit_working_hours",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BusinessId = table.Column<long>(type: "bigint", nullable: false),
                    OperationUnitId = table.Column<long>(type: "bigint", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    IsClosed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restaurant_operation_unit_working_hours", x => x.Id);
                    table.ForeignKey(
                        name: "FK_restaurant_operation_unit_working_hours_businesses_Business~",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_restaurant_operation_unit_working_hours_restaurant_operatio~",
                        column: x => x.OperationUnitId,
                        principalTable: "restaurant_operation_units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_operation_unit_working_hours_BusinessId",
                table: "restaurant_operation_unit_working_hours",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_operation_unit_working_hours_OperationUnitId",
                table: "restaurant_operation_unit_working_hours",
                column: "OperationUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_operation_unit_working_hours_OperationUnitId_Day~",
                table: "restaurant_operation_unit_working_hours",
                columns: new[] { "OperationUnitId", "DayOfWeek" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_operation_units_BusinessId",
                table: "restaurant_operation_units",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_operation_units_BusinessId_DisplayOrder",
                table: "restaurant_operation_units",
                columns: new[] { "BusinessId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_operation_units_BusinessId_UnitType",
                table: "restaurant_operation_units",
                columns: new[] { "BusinessId", "UnitType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "restaurant_operation_unit_working_hours");

            migrationBuilder.DropTable(
                name: "restaurant_operation_units");
        }
    }
}
