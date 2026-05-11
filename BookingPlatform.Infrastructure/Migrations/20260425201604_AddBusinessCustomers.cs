using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessCustomers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "business_customers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BusinessId = table.Column<long>(type: "bigint", nullable: false),
                    AppUserId = table.Column<long>(type: "bigint", nullable: true),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_customers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_business_customers_app_users_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "app_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_business_customers_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_business_customers_AppUserId",
                table: "business_customers",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_business_customers_BusinessId",
                table: "business_customers",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_business_customers_BusinessId_Email",
                table: "business_customers",
                columns: new[] { "BusinessId", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_business_customers_BusinessId_FullName",
                table: "business_customers",
                columns: new[] { "BusinessId", "FullName" });

            migrationBuilder.CreateIndex(
                name: "IX_business_customers_BusinessId_IsActive",
                table: "business_customers",
                columns: new[] { "BusinessId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_business_customers_BusinessId_Phone",
                table: "business_customers",
                columns: new[] { "BusinessId", "Phone" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "business_customers");
        }
    }
}
