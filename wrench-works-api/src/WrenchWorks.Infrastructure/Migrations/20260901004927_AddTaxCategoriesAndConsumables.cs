using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WrenchWorks.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTaxCategoriesAndConsumables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsConsumable",
                table: "InventoryItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "TaxRateCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TaxRateId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxRateCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaxRateCategories_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaxRateCategories_TaxRates_TaxRateId",
                        column: x => x.TaxRateId,
                        principalTable: "TaxRates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaxRateCategories_BusinessId_Category",
                table: "TaxRateCategories",
                columns: new[] { "BusinessId", "Category" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxRateCategories_TaxRateId",
                table: "TaxRateCategories",
                column: "TaxRateId");

            // Carry the existing mappings across BEFORE the columns holding them are
            // dropped. EF scaffolded the drops first, which would have silently unmapped
            // every configured rate — job totals would then quietly stop including tax,
            // with nothing to indicate why.
            migrationBuilder.Sql(@"
                INSERT INTO ""TaxRateCategories""
                    (""Id"", ""BusinessId"", ""Category"", ""TaxRateId"", ""CreatedAtUtc"", ""UpdatedAtUtc"")
                SELECT gen_random_uuid(), ""BusinessId"", 'Labour', ""Id"", now(), now()
                FROM ""TaxRates"" WHERE ""IsDefaultForLabour"" = true;
            ");

            migrationBuilder.Sql(@"
                INSERT INTO ""TaxRateCategories""
                    (""Id"", ""BusinessId"", ""Category"", ""TaxRateId"", ""CreatedAtUtc"", ""UpdatedAtUtc"")
                SELECT gen_random_uuid(), ""BusinessId"", 'Parts', ""Id"", now(), now()
                FROM ""TaxRates"" WHERE ""IsDefaultForParts"" = true;
            ");

            migrationBuilder.DropColumn(
                name: "IsDefaultForLabour",
                table: "TaxRates");

            migrationBuilder.DropColumn(
                name: "IsDefaultForParts",
                table: "TaxRates");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaxRateCategories");

            migrationBuilder.DropColumn(
                name: "IsConsumable",
                table: "InventoryItems");

            migrationBuilder.AddColumn<bool>(
                name: "IsDefaultForLabour",
                table: "TaxRates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDefaultForParts",
                table: "TaxRates",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
