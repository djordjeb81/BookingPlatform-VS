using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffServiceAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StaffServiceAssignments_services_ServiceId",
                table: "StaffServiceAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_StaffServiceAssignments_staff_members_StaffMemberId",
                table: "StaffServiceAssignments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_StaffServiceAssignments",
                table: "StaffServiceAssignments");

            migrationBuilder.RenameTable(
                name: "StaffServiceAssignments",
                newName: "staff_service_assignments");

            migrationBuilder.RenameIndex(
                name: "IX_StaffServiceAssignments_StaffMemberId_ServiceId",
                table: "staff_service_assignments",
                newName: "IX_staff_service_assignments_StaffMemberId_ServiceId");

            migrationBuilder.RenameIndex(
                name: "IX_StaffServiceAssignments_ServiceId",
                table: "staff_service_assignments",
                newName: "IX_staff_service_assignments_ServiceId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_staff_service_assignments",
                table: "staff_service_assignments",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "staff_resource_assignments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StaffMemberId = table.Column<long>(type: "bigint", nullable: false),
                    ResourceId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_resource_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_staff_resource_assignments_resources_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_staff_resource_assignments_staff_members_StaffMemberId",
                        column: x => x.StaffMemberId,
                        principalTable: "staff_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_staff_resource_assignments_ResourceId",
                table: "staff_resource_assignments",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_staff_resource_assignments_StaffMemberId_ResourceId",
                table: "staff_resource_assignments",
                columns: new[] { "StaffMemberId", "ResourceId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_staff_service_assignments_services_ServiceId",
                table: "staff_service_assignments",
                column: "ServiceId",
                principalTable: "services",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_staff_service_assignments_staff_members_StaffMemberId",
                table: "staff_service_assignments",
                column: "StaffMemberId",
                principalTable: "staff_members",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_staff_service_assignments_services_ServiceId",
                table: "staff_service_assignments");

            migrationBuilder.DropForeignKey(
                name: "FK_staff_service_assignments_staff_members_StaffMemberId",
                table: "staff_service_assignments");

            migrationBuilder.DropTable(
                name: "staff_resource_assignments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_staff_service_assignments",
                table: "staff_service_assignments");

            migrationBuilder.RenameTable(
                name: "staff_service_assignments",
                newName: "StaffServiceAssignments");

            migrationBuilder.RenameIndex(
                name: "IX_staff_service_assignments_StaffMemberId_ServiceId",
                table: "StaffServiceAssignments",
                newName: "IX_StaffServiceAssignments_StaffMemberId_ServiceId");

            migrationBuilder.RenameIndex(
                name: "IX_staff_service_assignments_ServiceId",
                table: "StaffServiceAssignments",
                newName: "IX_StaffServiceAssignments_ServiceId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_StaffServiceAssignments",
                table: "StaffServiceAssignments",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_StaffServiceAssignments_services_ServiceId",
                table: "StaffServiceAssignments",
                column: "ServiceId",
                principalTable: "services",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StaffServiceAssignments_staff_members_StaffMemberId",
                table: "StaffServiceAssignments",
                column: "StaffMemberId",
                principalTable: "staff_members",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
