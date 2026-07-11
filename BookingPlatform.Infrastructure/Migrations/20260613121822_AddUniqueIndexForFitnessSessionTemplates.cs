using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexForFitnessSessionTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX IF NOT EXISTS "UX_fitness_session_templates_no_duplicates"
                ON fitness_session_templates
                (
                    "BusinessId",
                    "FitnessRoomId",
                    "DayOfWeek",
                    "StartTime",
                    "DurationMin",
                    "SessionType",
                    COALESCE("FitnessClassTypeId", 0),
                    COALESCE("TrainerStaffMemberId", 0)
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "UX_fitness_session_templates_no_duplicates";
                """);
        }
    }
}