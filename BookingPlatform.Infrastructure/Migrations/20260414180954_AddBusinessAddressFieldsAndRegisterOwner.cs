using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessAddressFieldsAndRegisterOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ProgramVersion",
                table: "LicensedDevices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "LicenseToken",
                table: "LicensedDevices",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "HwidHash",
                table: "LicensedDevices",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ComputerName",
                table: "LicensedDevices",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "businesses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "businesses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GooglePlaceId",
                table: "businesses",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Latitude",
                table: "businesses",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Longitude",
                table: "businesses",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "businesses",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Street",
                table: "businesses",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StreetNumber",
                table: "businesses",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LicensedDevices_AppUserId_HwidHash",
                table: "LicensedDevices",
                columns: new[] { "AppUserId", "HwidHash" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LicensedDevices_app_users_AppUserId",
                table: "LicensedDevices",
                column: "AppUserId",
                principalTable: "app_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LicensedDevices_app_users_AppUserId",
                table: "LicensedDevices");

            migrationBuilder.DropIndex(
                name: "IX_LicensedDevices_AppUserId_HwidHash",
                table: "LicensedDevices");

            migrationBuilder.DropColumn(
                name: "City",
                table: "businesses");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "businesses");

            migrationBuilder.DropColumn(
                name: "GooglePlaceId",
                table: "businesses");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "businesses");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "businesses");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                table: "businesses");

            migrationBuilder.DropColumn(
                name: "Street",
                table: "businesses");

            migrationBuilder.DropColumn(
                name: "StreetNumber",
                table: "businesses");

            migrationBuilder.AlterColumn<string>(
                name: "ProgramVersion",
                table: "LicensedDevices",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "LicenseToken",
                table: "LicensedDevices",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "HwidHash",
                table: "LicensedDevices",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "ComputerName",
                table: "LicensedDevices",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);
        }
    }
}
