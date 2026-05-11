using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceResourceUsageStaffMembers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "service_resource_usage_staff_members",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ServiceResourceUsageId = table.Column<long>(type: "bigint", nullable: false),
                    StaffMemberId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_resource_usage_staff_members", x => x.Id);
                    table.ForeignKey(
                        name: "FK_service_resource_usage_staff_members_service_resource_usage~",
                        column: x => x.ServiceResourceUsageId,
                        principalTable: "service_resource_usages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_service_resource_usage_staff_members_staff_members_StaffMem~",
                        column: x => x.StaffMemberId,
                        principalTable: "staff_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_service_resource_usage_staff_members_ServiceResourceUsageId~",
                table: "service_resource_usage_staff_members",
                columns: new[] { "ServiceResourceUsageId", "StaffMemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_resource_usage_staff_members_StaffMemberId",
                table: "service_resource_usage_staff_members",
                column: "StaffMemberId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "service_resource_usage_staff_members");
        }
    }
}
