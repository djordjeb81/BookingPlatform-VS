using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFitnessModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "HasCustomerSeating",
                table: "business_feature_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.CreateTable(
                name: "fitness_class_types",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BusinessId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DefaultDurationMin = table.Column<int>(type: "integer", nullable: false, defaultValue: 60),
                    DefaultCapacity = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fitness_class_types", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fitness_class_types_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fitness_members",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BusinessId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerProfileId = table.Column<long>(type: "bigint", nullable: true),
                    BusinessCustomerId = table.Column<long>(type: "bigint", nullable: true),
                    AppUserId = table.Column<long>(type: "bigint", nullable: true),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MemberCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fitness_members", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fitness_members_app_users_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "app_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_fitness_members_business_customers_BusinessCustomerId",
                        column: x => x.BusinessCustomerId,
                        principalTable: "business_customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_fitness_members_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_fitness_members_customer_profiles_CustomerProfileId",
                        column: x => x.CustomerProfileId,
                        principalTable: "customer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "fitness_rooms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BusinessId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    AllowsGroupClasses = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    AllowsIndividualTraining = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fitness_rooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fitness_rooms_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fitness_settings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BusinessId = table.Column<long>(type: "bigint", nullable: false),
                    GroupClassesEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IndividualTrainingEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    MembershipsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    UnpaidMembershipBookingPolicy = table.Column<int>(type: "integer", nullable: false),
                    DefaultMembershipDurationDays = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                    AllowCustomerCancelBooking = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CustomerCancelDeadlineMinutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 120),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fitness_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fitness_settings_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fitness_membership_payments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BusinessId = table.Column<long>(type: "bigint", nullable: false),
                    FitnessMemberId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "RSD"),
                    PeriodStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fitness_membership_payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fitness_membership_payments_app_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "app_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_fitness_membership_payments_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_fitness_membership_payments_fitness_members_FitnessMemberId",
                        column: x => x.FitnessMemberId,
                        principalTable: "fitness_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fitness_sessions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BusinessId = table.Column<long>(type: "bigint", nullable: false),
                    FitnessRoomId = table.Column<long>(type: "bigint", nullable: false),
                    FitnessClassTypeId = table.Column<long>(type: "bigint", nullable: true),
                    TrainerStaffMemberId = table.Column<long>(type: "bigint", nullable: true),
                    SessionType = table.Column<int>(type: "integer", nullable: false),
                    StartAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fitness_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fitness_sessions_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_fitness_sessions_fitness_class_types_FitnessClassTypeId",
                        column: x => x.FitnessClassTypeId,
                        principalTable: "fitness_class_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_fitness_sessions_fitness_rooms_FitnessRoomId",
                        column: x => x.FitnessRoomId,
                        principalTable: "fitness_rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_fitness_sessions_staff_members_TrainerStaffMemberId",
                        column: x => x.TrainerStaffMemberId,
                        principalTable: "staff_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "fitness_session_bookings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BusinessId = table.Column<long>(type: "bigint", nullable: false),
                    FitnessSessionId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerProfileId = table.Column<long>(type: "bigint", nullable: true),
                    BusinessCustomerId = table.Column<long>(type: "bigint", nullable: true),
                    AppUserId = table.Column<long>(type: "bigint", nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CustomerPhone = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    MembershipWasActiveAtBooking = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    MembershipWarningText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AttendedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NoShowAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fitness_session_bookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fitness_session_bookings_app_users_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "app_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_fitness_session_bookings_business_customers_BusinessCustome~",
                        column: x => x.BusinessCustomerId,
                        principalTable: "business_customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_fitness_session_bookings_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_fitness_session_bookings_customer_profiles_CustomerProfileId",
                        column: x => x.CustomerProfileId,
                        principalTable: "customer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_fitness_session_bookings_fitness_sessions_FitnessSessionId",
                        column: x => x.FitnessSessionId,
                        principalTable: "fitness_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fitness_class_types_BusinessId",
                table: "fitness_class_types",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_fitness_class_types_BusinessId_Name",
                table: "fitness_class_types",
                columns: new[] { "BusinessId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fitness_members_AppUserId",
                table: "fitness_members",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_fitness_members_BusinessCustomerId",
                table: "fitness_members",
                column: "BusinessCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_fitness_members_BusinessId",
                table: "fitness_members",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_fitness_members_BusinessId_MemberCode",
                table: "fitness_members",
                columns: new[] { "BusinessId", "MemberCode" });

            migrationBuilder.CreateIndex(
                name: "IX_fitness_members_CustomerProfileId",
                table: "fitness_members",
                column: "CustomerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_fitness_membership_payments_BusinessId",
                table: "fitness_membership_payments",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_fitness_membership_payments_CreatedByUserId",
                table: "fitness_membership_payments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_fitness_membership_payments_FitnessMemberId",
                table: "fitness_membership_payments",
                column: "FitnessMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_fitness_membership_payments_FitnessMemberId_PeriodStartDate~",
                table: "fitness_membership_payments",
                columns: new[] { "FitnessMemberId", "PeriodStartDate", "PeriodEndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_fitness_rooms_BusinessId",
                table: "fitness_rooms",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_fitness_rooms_BusinessId_Name",
                table: "fitness_rooms",
                columns: new[] { "BusinessId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fitness_session_bookings_AppUserId",
                table: "fitness_session_bookings",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_fitness_session_bookings_BusinessCustomerId",
                table: "fitness_session_bookings",
                column: "BusinessCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_fitness_session_bookings_BusinessId",
                table: "fitness_session_bookings",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_fitness_session_bookings_CustomerProfileId",
                table: "fitness_session_bookings",
                column: "CustomerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_fitness_session_bookings_FitnessSessionId",
                table: "fitness_session_bookings",
                column: "FitnessSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_fitness_session_bookings_FitnessSessionId_Status",
                table: "fitness_session_bookings",
                columns: new[] { "FitnessSessionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_fitness_sessions_BusinessId",
                table: "fitness_sessions",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_fitness_sessions_BusinessId_StartAtUtc",
                table: "fitness_sessions",
                columns: new[] { "BusinessId", "StartAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_fitness_sessions_FitnessClassTypeId",
                table: "fitness_sessions",
                column: "FitnessClassTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_fitness_sessions_FitnessRoomId",
                table: "fitness_sessions",
                column: "FitnessRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_fitness_sessions_FitnessRoomId_StartAtUtc_EndAtUtc",
                table: "fitness_sessions",
                columns: new[] { "FitnessRoomId", "StartAtUtc", "EndAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_fitness_sessions_TrainerStaffMemberId",
                table: "fitness_sessions",
                column: "TrainerStaffMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_fitness_settings_BusinessId",
                table: "fitness_settings",
                column: "BusinessId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fitness_membership_payments");

            migrationBuilder.DropTable(
                name: "fitness_session_bookings");

            migrationBuilder.DropTable(
                name: "fitness_settings");

            migrationBuilder.DropTable(
                name: "fitness_members");

            migrationBuilder.DropTable(
                name: "fitness_sessions");

            migrationBuilder.DropTable(
                name: "fitness_class_types");

            migrationBuilder.DropTable(
                name: "fitness_rooms");

            migrationBuilder.AlterColumn<bool>(
                name: "HasCustomerSeating",
                table: "business_feature_settings",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);
        }
    }
}
