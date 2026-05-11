using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CustomerProfileId",
                table: "business_customers",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "customer_profiles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AppUserId = table.Column<long>(type: "bigint", nullable: true),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_profiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_business_customers_BusinessId_CustomerProfileId",
                table: "business_customers",
                columns: new[] { "BusinessId", "CustomerProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_business_customers_CustomerProfileId",
                table: "business_customers",
                column: "CustomerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_customer_profiles_AppUserId",
                table: "customer_profiles",
                column: "AppUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_business_customers_customer_profiles_CustomerProfileId",
                table: "business_customers",
                column: "CustomerProfileId",
                principalTable: "customer_profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Globalno pravilo:
            // jedan email = jedan globalni CustomerProfile.
            // Prazan/null email se ne ograničava.
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX IF NOT EXISTS ix_customer_profiles_email_unique
                ON customer_profiles (lower(trim("Email")))
                WHERE "Email" IS NOT NULL AND trim("Email") <> '';
            """);

            // 1) Za sve postojeće business_customers koji imaju email:
            // napravi jedan globalni profile po emailu.
            // Ako isti email postoji u više biznisa, dobiće jedan customer_profile.
            migrationBuilder.Sql("""
                INSERT INTO customer_profiles ("FullName", "Phone", "Email", "AppUserId", "CreatedAtUtc", "UpdatedAtUtc")
                SELECT
                    MIN("FullName") AS "FullName",
                    MIN("Phone") AS "Phone",
                    lower(trim("Email")) AS "Email",
                    MIN("AppUserId") AS "AppUserId",
                    NOW() AS "CreatedAtUtc",
                    NOW() AS "UpdatedAtUtc"
                FROM business_customers
                WHERE "Email" IS NOT NULL
                  AND trim("Email") <> ''
                GROUP BY lower(trim("Email"))
                ON CONFLICT DO NOTHING;
            """);

            // 2) Za postojeće business_customers bez emaila:
            // napravi poseban profile za svaki zapis.
            // Bez emaila ne možemo znati da li je ista osoba.
            migrationBuilder.Sql("""
                INSERT INTO customer_profiles ("FullName", "Phone", "Email", "AppUserId", "CreatedAtUtc", "UpdatedAtUtc")
                SELECT
                    "FullName",
                    "Phone",
                    NULL,
                    "AppUserId",
                    NOW(),
                    NOW()
                FROM business_customers
                WHERE "Email" IS NULL
                   OR trim("Email") = '';
            """);

            // 3) Poveži sve business_customers koji imaju email sa globalnim profile-om.
            migrationBuilder.Sql("""
                UPDATE business_customers bc
                SET "CustomerProfileId" = cp."Id"
                FROM customer_profiles cp
                WHERE bc."Email" IS NOT NULL
                  AND trim(bc."Email") <> ''
                  AND cp."Email" IS NOT NULL
                  AND lower(trim(bc."Email")) = lower(trim(cp."Email"));
            """);

            // 4) Poveži business_customers bez emaila.
            // Pošto je za svaki takav zapis napravljen poseban profile,
            // povezujemo ih redosledom preko row_number().
            migrationBuilder.Sql("""
                WITH bc_no_email AS (
                    SELECT
                        "Id",
                        row_number() OVER (ORDER BY "Id") AS rn
                    FROM business_customers
                    WHERE ("Email" IS NULL OR trim("Email") = '')
                      AND "CustomerProfileId" IS NULL
                ),
                cp_no_email AS (
                    SELECT
                        "Id",
                        row_number() OVER (ORDER BY "Id") AS rn
                    FROM customer_profiles
                    WHERE "Email" IS NULL
                )
                UPDATE business_customers bc
                SET "CustomerProfileId" = cp."Id"
                FROM bc_no_email bcn
                JOIN cp_no_email cp ON cp.rn = bcn.rn
                WHERE bc."Id" = bcn."Id";
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS ix_customer_profiles_email_unique;
            """);

            migrationBuilder.DropForeignKey(
                name: "FK_business_customers_customer_profiles_CustomerProfileId",
                table: "business_customers");

            migrationBuilder.DropTable(
                name: "customer_profiles");

            migrationBuilder.DropIndex(
                name: "IX_business_customers_BusinessId_CustomerProfileId",
                table: "business_customers");

            migrationBuilder.DropIndex(
                name: "IX_business_customers_CustomerProfileId",
                table: "business_customers");

            migrationBuilder.DropColumn(
                name: "CustomerProfileId",
                table: "business_customers");
        }
    }
}