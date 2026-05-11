using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceResourceUsages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ServiceStepResourceRequirements",
                table: "ServiceStepResourceRequirements");

            migrationBuilder.RenameTable(
                name: "ServiceStepResourceRequirements",
                newName: "ServiceStepResourceRequirement");

            migrationBuilder.RenameIndex(
                name: "IX_ServiceStepResourceRequirements_ServiceStepId_ResourceId",
                table: "ServiceStepResourceRequirement",
                newName: "IX_ServiceStepResourceRequirement_ServiceStepId_ResourceId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ServiceStepResourceRequirement",
                table: "ServiceStepResourceRequirement",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "ServiceResourceUsages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ServiceId = table.Column<long>(type: "bigint", nullable: false),
                    ResourceId = table.Column<long>(type: "bigint", nullable: false),
                    SequenceOrder = table.Column<int>(type: "integer", nullable: false),
                    UsageDurationMin = table.Column<int>(type: "integer", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceResourceUsages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceResourceUsages_ServiceId_SequenceOrder",
                table: "ServiceResourceUsages",
                columns: new[] { "ServiceId", "SequenceOrder" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceResourceUsages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ServiceStepResourceRequirement",
                table: "ServiceStepResourceRequirement");

            migrationBuilder.RenameTable(
                name: "ServiceStepResourceRequirement",
                newName: "ServiceStepResourceRequirements");

            migrationBuilder.RenameIndex(
                name: "IX_ServiceStepResourceRequirement_ServiceStepId_ResourceId",
                table: "ServiceStepResourceRequirements",
                newName: "IX_ServiceStepResourceRequirements_ServiceStepId_ResourceId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ServiceStepResourceRequirements",
                table: "ServiceStepResourceRequirements",
                column: "Id");
        }
    }
}
