using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessActivityNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "business_activity_notifications",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BusinessId = table.Column<long>(type: "bigint", nullable: false),
                    RecipientType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RecipientKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RecipientAppUserId = table.Column<long>(type: "bigint", nullable: true),
                    RecipientCustomerProfileId = table.Column<long>(type: "bigint", nullable: true),
                    RecipientStaffMemberId = table.Column<long>(type: "bigint", nullable: true),
                    RecipientOperationUnitId = table.Column<long>(type: "bigint", nullable: true),
                    Domain = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Kind = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ActivityKey = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    Title = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    MainText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PreviewText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsSeen = table.Column<bool>(type: "boolean", nullable: false),
                    SeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SeenByUserId = table.Column<long>(type: "bigint", nullable: true),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    SnoozedUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SnoozedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    LastReminderAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SortAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AppointmentId = table.Column<long>(type: "bigint", nullable: true),
                    ChangeRequestId = table.Column<long>(type: "bigint", nullable: true),
                    RestaurantOrderId = table.Column<long>(type: "bigint", nullable: true),
                    RestaurantTableReservationId = table.Column<long>(type: "bigint", nullable: true),
                    RestaurantAreaReservationId = table.Column<long>(type: "bigint", nullable: true),
                    ConversationId = table.Column<long>(type: "bigint", nullable: true),
                    ChatMessageId = table.Column<long>(type: "bigint", nullable: true),
                    SystemAlarmTriggerId = table.Column<long>(type: "bigint", nullable: true),
                    FitnessSessionId = table.Column<long>(type: "bigint", nullable: true),
                    FitnessSessionBookingId = table.Column<long>(type: "bigint", nullable: true),
                    FitnessMemberId = table.Column<long>(type: "bigint", nullable: true),
                    FitnessMemberSessionDebtId = table.Column<long>(type: "bigint", nullable: true),
                    CustomerProfileId = table.Column<long>(type: "bigint", nullable: true),
                    BusinessCustomerId = table.Column<long>(type: "bigint", nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CustomerPhone = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_activity_notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_business_activity_notifications_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_business_activity_notifications_BusinessId",
                table: "business_activity_notifications",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_business_activity_notifications_BusinessId_RecipientKey",
                table: "business_activity_notifications",
                columns: new[] { "BusinessId", "RecipientKey" });

            migrationBuilder.CreateIndex(
                name: "IX_business_activity_notifications_BusinessId_RecipientKey_Act~",
                table: "business_activity_notifications",
                columns: new[] { "BusinessId", "RecipientKey", "ActivityKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_business_activity_notifications_BusinessId_RecipientKey_IsR~",
                table: "business_activity_notifications",
                columns: new[] { "BusinessId", "RecipientKey", "IsResolved" });

            migrationBuilder.CreateIndex(
                name: "IX_business_activity_notifications_BusinessId_RecipientKey_IsS~",
                table: "business_activity_notifications",
                columns: new[] { "BusinessId", "RecipientKey", "IsSeen" });

            migrationBuilder.CreateIndex(
                name: "IX_business_activity_notifications_BusinessId_RecipientKey_Sno~",
                table: "business_activity_notifications",
                columns: new[] { "BusinessId", "RecipientKey", "SnoozedUntilUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_business_activity_notifications_BusinessId_RecipientKey_Sor~",
                table: "business_activity_notifications",
                columns: new[] { "BusinessId", "RecipientKey", "SortAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "business_activity_notifications");
        }
    }
}
