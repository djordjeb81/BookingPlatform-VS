using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessBookingModeAndFeatureSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BookingMode",
                table: "businesses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "business_feature_settings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BusinessId = table.Column<long>(type: "bigint", nullable: false),
                    ServiceAppointmentsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    TableReservationsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    FoodOrdersEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DrinkOrdersEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TakeawayOrdersEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeliveryOrdersEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    EventHallReservationsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    AccommodationEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ReviewsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_feature_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_business_feature_settings_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_business_feature_settings_BusinessId",
                table: "business_feature_settings",
                column: "BusinessId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "business_feature_settings");

            migrationBuilder.DropColumn(
                name: "BookingMode",
                table: "businesses");
        }
    }
}
