using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTimeOffBlocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "time_off_blocks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BusinessId = table.Column<long>(type: "bigint", nullable: false),
                    StaffMemberId = table.Column<long>(type: "bigint", nullable: true),
                    StartAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BlockType = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_time_off_blocks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_time_off_blocks_BusinessId",
                table: "time_off_blocks",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_time_off_blocks_EndAtUtc",
                table: "time_off_blocks",
                column: "EndAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_time_off_blocks_StaffMemberId",
                table: "time_off_blocks",
                column: "StaffMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_time_off_blocks_StartAtUtc",
                table: "time_off_blocks",
                column: "StartAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "time_off_blocks");
        }
    }
}
