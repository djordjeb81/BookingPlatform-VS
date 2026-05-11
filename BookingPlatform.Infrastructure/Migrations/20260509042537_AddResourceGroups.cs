using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ResourceGroupId",
                table: "resources",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "resource_groups",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BusinessId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resource_groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_resource_groups_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_resources_ResourceGroupId",
                table: "resources",
                column: "ResourceGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_resource_groups_BusinessId_Name",
                table: "resource_groups",
                columns: new[] { "BusinessId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_resources_resource_groups_ResourceGroupId",
                table: "resources",
                column: "ResourceGroupId",
                principalTable: "resource_groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_resources_resource_groups_ResourceGroupId",
                table: "resources");

            migrationBuilder.DropTable(
                name: "resource_groups");

            migrationBuilder.DropIndex(
                name: "IX_resources_ResourceGroupId",
                table: "resources");

            migrationBuilder.DropColumn(
                name: "ResourceGroupId",
                table: "resources");
        }
    }
}
