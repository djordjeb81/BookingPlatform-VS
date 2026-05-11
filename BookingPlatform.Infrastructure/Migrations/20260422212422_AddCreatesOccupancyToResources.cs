using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    public partial class AddCreatesOccupancyToResources : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE resources
                SET "CreatesOccupancy" = TRUE
                WHERE "CreatesOccupancy" = FALSE;
            """);

            migrationBuilder.Sql("""
                ALTER TABLE resources
                ALTER COLUMN "CreatesOccupancy" SET DEFAULT TRUE;
            """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE resources
                ALTER COLUMN "CreatesOccupancy" SET DEFAULT FALSE;
            """);
        }
    }
}