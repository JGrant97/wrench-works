using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WrenchWorks.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTaxRatesAndLineSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TaxAmount",
                table: "JobPartLines",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "TaxRateId",
                table: "JobPartLines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxRatePercent",
                table: "JobPartLines",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxAmount",
                table: "JobLaborLines",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "TaxRateId",
                table: "JobLaborLines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxRatePercent",
                table: "JobLaborLines",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsTaxExempt",
                table: "Customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TaxExemptionReference",
                table: "Customers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PricesIncludeTax",
                table: "Businesses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TaxLabel",
                table: "Businesses",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Tax");

            migrationBuilder.AddColumn<string>(
                name: "TaxRegistrationNumber",
                table: "Businesses",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TaxRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    IsDefaultForLabour = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefaultForParts = table.Column<bool>(type: "boolean", nullable: false),
                    ArchivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaxRates_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaxRateComponents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxRateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxRateComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaxRateComponents_TaxRates_TaxRateId",
                        column: x => x.TaxRateId,
                        principalTable: "TaxRates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaxRateComponents_TaxRateId",
                table: "TaxRateComponents",
                column: "TaxRateId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxRates_BusinessId_ArchivedAtUtc",
                table: "TaxRates",
                columns: new[] { "BusinessId", "ArchivedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaxRateComponents");

            migrationBuilder.DropTable(
                name: "TaxRates");

            migrationBuilder.DropColumn(
                name: "TaxAmount",
                table: "JobPartLines");

            migrationBuilder.DropColumn(
                name: "TaxRateId",
                table: "JobPartLines");

            migrationBuilder.DropColumn(
                name: "TaxRatePercent",
                table: "JobPartLines");

            migrationBuilder.DropColumn(
                name: "TaxAmount",
                table: "JobLaborLines");

            migrationBuilder.DropColumn(
                name: "TaxRateId",
                table: "JobLaborLines");

            migrationBuilder.DropColumn(
                name: "TaxRatePercent",
                table: "JobLaborLines");

            migrationBuilder.DropColumn(
                name: "IsTaxExempt",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "TaxExemptionReference",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PricesIncludeTax",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "TaxLabel",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "TaxRegistrationNumber",
                table: "Businesses");
        }
    }
}
