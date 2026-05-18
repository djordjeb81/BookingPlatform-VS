using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantTableSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "restaurant_areas",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BusinessId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    CanvasWidth = table.Column<int>(type: "integer", nullable: false, defaultValue: 1000),
                    CanvasHeight = table.Column<int>(type: "integer", nullable: false, defaultValue: 1000),
                    BoundaryPointsJson = table.Column<string>(type: "jsonb", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsReservableAsWhole = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    WholeAreaResourceId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restaurant_areas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_restaurant_areas_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "restaurant_layout_elements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RestaurantAreaId = table.Column<long>(type: "bigint", nullable: false),
                    ElementType = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    X = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Y = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Width = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Height = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    RotationDeg = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ShapeType = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    PointsJson = table.Column<string>(type: "jsonb", nullable: true),
                    IsObstacle = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restaurant_layout_elements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_restaurant_layout_elements_restaurant_areas_RestaurantAreaId",
                        column: x => x.RestaurantAreaId,
                        principalTable: "restaurant_areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "restaurant_table_sessions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BusinessId = table.Column<long>(type: "bigint", nullable: false),
                    RestaurantAreaId = table.Column<long>(type: "bigint", nullable: false),
                    TableResourceId = table.Column<long>(type: "bigint", nullable: false),
                    PartySize = table.Column<int>(type: "integer", nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CustomerPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReleasedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restaurant_table_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_restaurant_table_sessions_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_restaurant_table_sessions_resources_TableResourceId",
                        column: x => x.TableResourceId,
                        principalTable: "resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_restaurant_table_sessions_restaurant_areas_RestaurantAreaId",
                        column: x => x.RestaurantAreaId,
                        principalTable: "restaurant_areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_areas_BusinessId",
                table: "restaurant_areas",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_areas_BusinessId_Name",
                table: "restaurant_areas",
                columns: new[] { "BusinessId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_layout_elements_RestaurantAreaId",
                table: "restaurant_layout_elements",
                column: "RestaurantAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_table_sessions_BusinessId",
                table: "restaurant_table_sessions",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_table_sessions_BusinessId_TableResourceId_Status",
                table: "restaurant_table_sessions",
                columns: new[] { "BusinessId", "TableResourceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_table_sessions_RestaurantAreaId",
                table: "restaurant_table_sessions",
                column: "RestaurantAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_table_sessions_TableResourceId",
                table: "restaurant_table_sessions",
                column: "TableResourceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "restaurant_layout_elements");

            migrationBuilder.DropTable(
                name: "restaurant_table_sessions");

            migrationBuilder.DropTable(
                name: "restaurant_areas");
        }
    }
}
