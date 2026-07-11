using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFitnessMemberSessionDebts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fitness_member_session_debts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BusinessId = table.Column<long>(type: "bigint", nullable: false),
                    FitnessMemberId = table.Column<long>(type: "bigint", nullable: false),
                    FitnessSessionId = table.Column<long>(type: "bigint", nullable: false),
                    FitnessClassTypeId = table.Column<long>(type: "bigint", nullable: true),
                    FitnessMemberTrainingPassId = table.Column<long>(type: "bigint", nullable: true),
                    SessionsCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    SettledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VoidedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fitness_member_session_debts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fitness_member_session_debts_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_fitness_member_session_debts_fitness_class_types_FitnessCla~",
                        column: x => x.FitnessClassTypeId,
                        principalTable: "fitness_class_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_fitness_member_session_debts_fitness_member_training_passes~",
                        column: x => x.FitnessMemberTrainingPassId,
                        principalTable: "fitness_member_training_passes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_fitness_member_session_debts_fitness_members_FitnessMemberId",
                        column: x => x.FitnessMemberId,
                        principalTable: "fitness_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_fitness_member_session_debts_fitness_sessions_FitnessSessio~",
                        column: x => x.FitnessSessionId,
                        principalTable: "fitness_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fitness_member_session_debts_BusinessId",
                table: "fitness_member_session_debts",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_fitness_member_session_debts_BusinessId_Status",
                table: "fitness_member_session_debts",
                columns: new[] { "BusinessId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_fitness_member_session_debts_FitnessClassTypeId",
                table: "fitness_member_session_debts",
                column: "FitnessClassTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_fitness_member_session_debts_FitnessMemberId",
                table: "fitness_member_session_debts",
                column: "FitnessMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_fitness_member_session_debts_FitnessMemberId_Status",
                table: "fitness_member_session_debts",
                columns: new[] { "FitnessMemberId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_fitness_member_session_debts_FitnessMemberTrainingPassId",
                table: "fitness_member_session_debts",
                column: "FitnessMemberTrainingPassId");

            migrationBuilder.CreateIndex(
                name: "IX_fitness_member_session_debts_FitnessSessionId",
                table: "fitness_member_session_debts",
                column: "FitnessSessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fitness_member_session_debts");
        }
    }
}
