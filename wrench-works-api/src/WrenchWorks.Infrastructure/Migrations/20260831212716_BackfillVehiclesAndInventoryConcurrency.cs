using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WrenchWorks.Infrastructure.Migrations
{
    /// <summary>
    /// Two corrections, both from the 31 Aug 2026 review pass.
    ///
    /// 1. AddVehicleCatalogue added DisplayName as nullable and never populated it — no
    ///    Sql(), no UPDATE. The one existing vehicle was backfilled by a hand-run statement
    ///    against the dev database, which docs/vehicle-catalogue.md recorded as done. True
    ///    on one machine, false everywhere else. This runs that backfill properly.
    ///
    /// 2. InventoryItems carried a real "RowVersion" bigint column that nothing ever read,
    ///    because the entity was the one BaseEntity descendant never given .IsRowVersion().
    ///    The five entities that do have it map to Postgres's xmin system column and have
    ///    no such column of their own. So the fix is to DROP the dead column, not rename
    ///    it: xmin already exists and is reserved, and EF's scaffolded
    ///    RenameColumn("RowVersion" -> "xmin") would have failed on execution.
    /// </summary>
    public partial class BackfillVehiclesAndInventoryConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Vehicles created before the catalogue have no DisplayName snapshot, and every
            // list view reads that field. Rebuild it from the deprecated free-text columns,
            // which still hold what the row was created with. NULLIF(...,'') keeps a row
            // with no make or model from becoming an empty string that renders blank.
            migrationBuilder.Sql(@"
                UPDATE ""Vehicles""
                SET ""DisplayName"" = COALESCE(
                    NULLIF(TRIM(CONCAT_WS(' ', ""Year""::text, ""Make"", ""Model"")), ''),
                    'Unnamed vehicle')
                WHERE ""DisplayName"" IS NULL;
            ");

            // Dead column: never mapped as a concurrency token, never read by anything.
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "InventoryItems");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "RowVersion",
                table: "InventoryItems",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            // The DisplayName backfill is deliberately not reversed. Reverting it would mean
            // nulling a column whose pre-migration state we cannot distinguish from a value
            // written since, and a blank display name is strictly worse than a stale one.
        }
    }
}
