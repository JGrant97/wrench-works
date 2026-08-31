using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WrenchWorks.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleCatalogue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ColourId",
                table: "Vehicles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "Vehicles",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VariantId",
                table: "Vehicles",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VehicleColours",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    HexCode = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleColours", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VehicleMakes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    VpicMakeId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleMakes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VehicleModels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MakeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    VpicModelId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleModels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleModels_VehicleMakes_MakeId",
                        column: x => x.MakeId,
                        principalTable: "VehicleMakes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VehicleVariants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModelId = table.Column<Guid>(type: "uuid", nullable: false),
                    YearFrom = table.Column<int>(type: "integer", nullable: false),
                    YearTo = table.Column<int>(type: "integer", nullable: false),
                    Trim = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BodyStyle = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    EngineDisplacementL = table.Column<decimal>(type: "numeric(3,1)", precision: 3, scale: 1, nullable: true),
                    EngineCylinders = table.Column<int>(type: "integer", nullable: true),
                    FuelType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Transmission = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DriveType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleVariants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleVariants_VehicleModels_ModelId",
                        column: x => x.ModelId,
                        principalTable: "VehicleModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_ColourId",
                table: "Vehicles",
                column: "ColourId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_VariantId",
                table: "Vehicles",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleColours_Name",
                table: "VehicleColours",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VehicleMakes_Name",
                table: "VehicleMakes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VehicleMakes_VpicMakeId",
                table: "VehicleMakes",
                column: "VpicMakeId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleModels_MakeId_Name",
                table: "VehicleModels",
                columns: new[] { "MakeId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VehicleVariants_ModelId_YearFrom_YearTo",
                table: "VehicleVariants",
                columns: new[] { "ModelId", "YearFrom", "YearTo" });

            migrationBuilder.AddForeignKey(
                name: "FK_Vehicles_VehicleColours_ColourId",
                table: "Vehicles",
                column: "ColourId",
                principalTable: "VehicleColours",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Vehicles_VehicleVariants_VariantId",
                table: "Vehicles",
                column: "VariantId",
                principalTable: "VehicleVariants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Vehicles_VehicleColours_ColourId",
                table: "Vehicles");

            migrationBuilder.DropForeignKey(
                name: "FK_Vehicles_VehicleVariants_VariantId",
                table: "Vehicles");

            migrationBuilder.DropTable(
                name: "VehicleColours");

            migrationBuilder.DropTable(
                name: "VehicleVariants");

            migrationBuilder.DropTable(
                name: "VehicleModels");

            migrationBuilder.DropTable(
                name: "VehicleMakes");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_ColourId",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_VariantId",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "ColourId",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "VariantId",
                table: "Vehicles");
        }
    }
}
