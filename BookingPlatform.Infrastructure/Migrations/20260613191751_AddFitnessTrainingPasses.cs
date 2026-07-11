using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFitnessTrainingPasses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ConsumesTrainingPassSession",
                table: "fitness_session_bookings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<long>(
                name: "FitnessMemberTrainingPassId",
                table: "fitness_session_bookings",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "fitness_membership_plans",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BusinessId = table.Column<long>(type: "bigint", nullable: false),
                    FitnessClassTypeId = table.Column<long>(type: "bigint", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TotalSessions = table.Column<int>(type: "integer", nullable: true),
                    WeeklySessionLimit = table.Column<int>(type: "integer", nullable: true),
                    DefaultValidityDays = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "RSD"),
                    UnusedSessionsCarryOver = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fitness_membership_plans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fitness_membership_plans_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_fitness_membership_plans_fitness_class_types_FitnessClassTy~",
                        column: x => x.FitnessClassTypeId,
                        principalTable: "fitness_class_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "fitness_member_training_passes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BusinessId = table.Column<long>(type: "bigint", nullable: false),
                    FitnessMemberId = table.Column<long>(type: "bigint", nullable: false),
                    FitnessMembershipPlanId = table.Column<long>(type: "bigint", nullable: false),
                    FitnessClassTypeId = table.Column<long>(type: "bigint", nullable: true),
                    PlanNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FitnessClassTypeNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ValidFromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalSessions = table.Column<int>(type: "integer", nullable: true),
                    WeeklySessionLimit = table.Column<int>(type: "integer", nullable: true),
                    PricePaid = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "RSD"),
                    PaidAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fitness_member_training_passes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fitness_member_training_passes_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_fitness_member_training_passes_fitness_class_types_FitnessC~",
                        column: x => x.FitnessClassTypeId,
                        principalTable: "fitness_class_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_fitness_member_training_passes_fitness_members_FitnessMembe~",
                        column: x => x.FitnessMemberId,
                        principalTable: "fitness_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_fitness_member_training_passes_fitness_membership_plans_Fit~",
                        column: x => x.FitnessMembershipPlanId,
                        principalTable: "fitness_membership_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fitness_session_bookings_FitnessMemberTrainingPassId",
                table: "fitness_session_bookings",
                column: "FitnessMemberTrainingPassId");

            migrationBuilder.CreateIndex(
                name: "IX_fitness_member_training_passes_BusinessId",
                table: "fitness_member_training_passes",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_fitness_member_training_passes_BusinessId_ValidFromDate_Val~",
                table: "fitness_member_training_passes",
                columns: new[] { "BusinessId", "ValidFromDate", "ValidToDate" });

            migrationBuilder.CreateIndex(
                name: "IX_fitness_member_training_passes_FitnessClassTypeId",
                table: "fitness_member_training_passes",
                column: "FitnessClassTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_fitness_member_training_passes_FitnessMemberId",
                table: "fitness_member_training_passes",
                column: "FitnessMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_fitness_member_training_passes_FitnessMemberId_ValidFromDat~",
                table: "fitness_member_training_passes",
                columns: new[] { "FitnessMemberId", "ValidFromDate", "ValidToDate" });

            migrationBuilder.CreateIndex(
                name: "IX_fitness_member_training_passes_FitnessMembershipPlanId",
                table: "fitness_member_training_passes",
                column: "FitnessMembershipPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_fitness_membership_plans_BusinessId",
                table: "fitness_membership_plans",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_fitness_membership_plans_BusinessId_DisplayOrder",
                table: "fitness_membership_plans",
                columns: new[] { "BusinessId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_fitness_membership_plans_BusinessId_IsActive",
                table: "fitness_membership_plans",
                columns: new[] { "BusinessId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_fitness_membership_plans_FitnessClassTypeId",
                table: "fitness_membership_plans",
                column: "FitnessClassTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_fitness_session_bookings_fitness_member_training_passes_Fit~",
                table: "fitness_session_bookings",
                column: "FitnessMemberTrainingPassId",
                principalTable: "fitness_member_training_passes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_fitness_session_bookings_fitness_member_training_passes_Fit~",
                table: "fitness_session_bookings");

            migrationBuilder.DropTable(
                name: "fitness_member_training_passes");

            migrationBuilder.DropTable(
                name: "fitness_membership_plans");

            migrationBuilder.DropIndex(
                name: "IX_fitness_session_bookings_FitnessMemberTrainingPassId",
                table: "fitness_session_bookings");

            migrationBuilder.DropColumn(
                name: "ConsumesTrainingPassSession",
                table: "fitness_session_bookings");

            migrationBuilder.DropColumn(
                name: "FitnessMemberTrainingPassId",
                table: "fitness_session_bookings");
        }
    }
}
