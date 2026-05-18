using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceLayoutFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "resources",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<string>(
                name: "CustomerActionText",
                table: "resources",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "CreatesOccupancy",
                table: "resources",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AddColumn<decimal>(
                name: "LayoutHeight",
                table: "resources",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LayoutPointsJson",
                table: "resources",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LayoutRotationDeg",
                table: "resources",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LayoutShape",
                table: "resources",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "LayoutWidth",
                table: "resources",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LayoutX",
                table: "resources",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LayoutY",
                table: "resources",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RestaurantAreaId",
                table: "resources",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_resources_BusinessId",
                table: "resources",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_resources_RestaurantAreaId",
                table: "resources",
                column: "RestaurantAreaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_resources_BusinessId",
                table: "resources");

            migrationBuilder.DropIndex(
                name: "IX_resources_RestaurantAreaId",
                table: "resources");

            migrationBuilder.DropColumn(
                name: "LayoutHeight",
                table: "resources");

            migrationBuilder.DropColumn(
                name: "LayoutPointsJson",
                table: "resources");

            migrationBuilder.DropColumn(
                name: "LayoutRotationDeg",
                table: "resources");

            migrationBuilder.DropColumn(
                name: "LayoutShape",
                table: "resources");

            migrationBuilder.DropColumn(
                name: "LayoutWidth",
                table: "resources");

            migrationBuilder.DropColumn(
                name: "LayoutX",
                table: "resources");

            migrationBuilder.DropColumn(
                name: "LayoutY",
                table: "resources");

            migrationBuilder.DropColumn(
                name: "RestaurantAreaId",
                table: "resources");

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "resources",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<string>(
                name: "CustomerActionText",
                table: "resources",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "CreatesOccupancy",
                table: "resources",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);
        }
    }
}
