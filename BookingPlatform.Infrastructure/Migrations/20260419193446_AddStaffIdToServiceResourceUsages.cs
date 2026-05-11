using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffIdToServiceResourceUsages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ServiceResourceUsages",
                table: "ServiceResourceUsages");

            migrationBuilder.RenameTable(
                name: "ServiceResourceUsages",
                newName: "service_resource_usages");

            migrationBuilder.AddColumn<long>(
                name: "StaffId",
                table: "service_resource_usages",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_service_resource_usages",
                table: "service_resource_usages",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_service_resource_usages_StaffId",
                table: "service_resource_usages",
                column: "StaffId");

            migrationBuilder.AddForeignKey(
                name: "FK_service_resource_usages_staff_members_StaffId",
                table: "service_resource_usages",
                column: "StaffId",
                principalTable: "staff_members",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_service_resource_usages_staff_members_StaffId",
                table: "service_resource_usages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_service_resource_usages",
                table: "service_resource_usages");

            migrationBuilder.DropIndex(
                name: "IX_service_resource_usages_StaffId",
                table: "service_resource_usages");

            migrationBuilder.DropColumn(
                name: "StaffId",
                table: "service_resource_usages");

            migrationBuilder.RenameTable(
                name: "service_resource_usages",
                newName: "ServiceResourceUsages");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ServiceResourceUsages",
                table: "ServiceResourceUsages",
                column: "Id");
        }
    }
}
