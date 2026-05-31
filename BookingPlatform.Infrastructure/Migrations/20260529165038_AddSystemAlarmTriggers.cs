using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemAlarmTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "system_alarm_triggers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BusinessId = table.Column<long>(type: "bigint", nullable: false),
                    Domain = table.Column<int>(type: "integer", nullable: false),
                    AlarmType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    TargetUserId = table.Column<long>(type: "bigint", nullable: true),
                    TargetOperationUnitId = table.Column<long>(type: "bigint", nullable: true),
                    RelatedOrderId = table.Column<long>(type: "bigint", nullable: true),
                    RelatedAppointmentId = table.Column<long>(type: "bigint", nullable: true),
                    RelatedChatConversationId = table.Column<long>(type: "bigint", nullable: true),
                    RelatedChatMessageId = table.Column<long>(type: "bigint", nullable: true),
                    TriggerAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FiredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StoppedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SnoozedUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SoundKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsUrgent = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    RequiresUserAction = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ActionKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_alarm_triggers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_system_alarm_triggers_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_system_alarm_triggers_BusinessId",
                table: "system_alarm_triggers",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_system_alarm_triggers_BusinessId_Domain_AlarmType",
                table: "system_alarm_triggers",
                columns: new[] { "BusinessId", "Domain", "AlarmType" });

            migrationBuilder.CreateIndex(
                name: "IX_system_alarm_triggers_BusinessId_Status_TriggerAtUtc",
                table: "system_alarm_triggers",
                columns: new[] { "BusinessId", "Status", "TriggerAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_system_alarm_triggers_RelatedAppointmentId",
                table: "system_alarm_triggers",
                column: "RelatedAppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_system_alarm_triggers_RelatedChatConversationId",
                table: "system_alarm_triggers",
                column: "RelatedChatConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_system_alarm_triggers_RelatedOrderId",
                table: "system_alarm_triggers",
                column: "RelatedOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_system_alarm_triggers_TargetOperationUnitId",
                table: "system_alarm_triggers",
                column: "TargetOperationUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_system_alarm_triggers_TargetUserId",
                table: "system_alarm_triggers",
                column: "TargetUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "system_alarm_triggers");
        }
    }
}
