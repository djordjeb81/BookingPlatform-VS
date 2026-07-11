using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFitnessWorkingHoursAndSessionTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "FitnessSessionTemplateId",
                table: "fitness_sessions",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "fitness_business_working_hours",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BusinessId = table.Column<long>(type: "bigint", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    IsClosed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    OpenTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    CloseTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fitness_business_working_hours", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fitness_business_working_hours_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fitness_room_working_hours",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BusinessId = table.Column<long>(type: "bigint", nullable: false),
                    FitnessRoomId = table.Column<long>(type: "bigint", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    IsClosed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    OpenTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    CloseTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fitness_room_working_hours", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fitness_room_working_hours_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_fitness_room_working_hours_fitness_rooms_FitnessRoomId",
                        column: x => x.FitnessRoomId,
                        principalTable: "fitness_rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fitness_session_templates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BusinessId = table.Column<long>(type: "bigint", nullable: false),
                    FitnessRoomId = table.Column<long>(type: "bigint", nullable: false),
                    FitnessClassTypeId = table.Column<long>(type: "bigint", nullable: true),
                    TrainerStaffMemberId = table.Column<long>(type: "bigint", nullable: true),
                    SessionType = table.Column<int>(type: "integer", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    DurationMin = table.Column<int>(type: "integer", nullable: false, defaultValue: 60),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ValidFromDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ValidToDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fitness_session_templates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fitness_session_templates_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_fitness_session_templates_fitness_class_types_FitnessClassT~",
                        column: x => x.FitnessClassTypeId,
                        principalTable: "fitness_class_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_fitness_session_templates_fitness_rooms_FitnessRoomId",
                        column: x => x.FitnessRoomId,
                        principalTable: "fitness_rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_fitness_session_templates_staff_members_TrainerStaffMemberId",
                        column: x => x.TrainerStaffMemberId,
                        principalTable: "staff_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fitness_sessions_FitnessSessionTemplateId",
                table: "fitness_sessions",
                column: "FitnessSessionTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_fitness_business_working_hours_BusinessId",
                table: "fitness_business_working_hours",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_fitness_business_working_hours_BusinessId_DayOfWeek",
                table: "fitness_business_working_hours",
                columns: new[] { "BusinessId", "DayOfWeek" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fitness_room_working_hours_BusinessId",
                table: "fitness_room_working_hours",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_fitness_room_working_hours_FitnessRoomId",
                table: "fitness_room_working_hours",
                column: "FitnessRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_fitness_room_working_hours_FitnessRoomId_DayOfWeek",
                table: "fitness_room_working_hours",
                columns: new[] { "FitnessRoomId", "DayOfWeek" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fitness_session_templates_BusinessId",
                table: "fitness_session_templates",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_fitness_session_templates_FitnessClassTypeId",
                table: "fitness_session_templates",
                column: "FitnessClassTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_fitness_session_templates_FitnessRoomId",
                table: "fitness_session_templates",
                column: "FitnessRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_fitness_session_templates_FitnessRoomId_DayOfWeek_StartTime",
                table: "fitness_session_templates",
                columns: new[] { "FitnessRoomId", "DayOfWeek", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_fitness_session_templates_TrainerStaffMemberId",
                table: "fitness_session_templates",
                column: "TrainerStaffMemberId");

            migrationBuilder.AddForeignKey(
                name: "FK_fitness_sessions_fitness_session_templates_FitnessSessionTe~",
                table: "fitness_sessions",
                column: "FitnessSessionTemplateId",
                principalTable: "fitness_session_templates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_fitness_sessions_fitness_session_templates_FitnessSessionTe~",
                table: "fitness_sessions");

            migrationBuilder.DropTable(
                name: "fitness_business_working_hours");

            migrationBuilder.DropTable(
                name: "fitness_room_working_hours");

            migrationBuilder.DropTable(
                name: "fitness_session_templates");

            migrationBuilder.DropIndex(
                name: "IX_fitness_sessions_FitnessSessionTemplateId",
                table: "fitness_sessions");

            migrationBuilder.DropColumn(
                name: "FitnessSessionTemplateId",
                table: "fitness_sessions");
        }
    }
}
