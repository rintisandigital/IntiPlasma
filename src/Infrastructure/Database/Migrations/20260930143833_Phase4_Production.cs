using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Phase4_Production : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "production");

            migrationBuilder.AddColumn<DateOnly>(
                name: "closed_date",
                schema: "partnership",
                table: "production_cycles",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "closing_performance",
                schema: "partnership",
                table: "production_cycles",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "harvested_birds",
                schema: "partnership",
                table: "production_cycles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "harvested_weight_kg",
                schema: "partnership",
                table: "production_cycles",
                type: "numeric(14,3)",
                precision: 14,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "total_culling",
                schema: "partnership",
                table: "production_cycles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "total_mortality",
                schema: "partnership",
                table: "production_cycles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "cycle_harvests",
                schema: "partnership",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cycle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    age_days = table.Column<int>(type: "integer", nullable: false),
                    birds = table.Column<int>(type: "integer", nullable: false),
                    weight_kg = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cycle_harvests", x => x.id);
                    table.ForeignKey(
                        name: "fk_cycle_harvests_production_cycles_cycle_id",
                        column: x => x.cycle_id,
                        principalSchema: "partnership",
                        principalTable: "production_cycles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "daily_recordings",
                schema: "production",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cycle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    coop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    age_days = table.Column<int>(type: "integer", nullable: false),
                    mortality = table.Column<int>(type: "integer", nullable: false),
                    culling = table.Column<int>(type: "integer", nullable: false),
                    average_body_weight_gram = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    revision_number = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_daily_recordings", x => x.id);
                    table.ForeignKey(
                        name: "fk_daily_recordings_branches_branch_id",
                        column: x => x.branch_id,
                        principalSchema: "master",
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_daily_recordings_coops_coop_id",
                        column: x => x.coop_id,
                        principalSchema: "master",
                        principalTable: "coops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_daily_recordings_production_cycles_cycle_id",
                        column: x => x.cycle_id,
                        principalSchema: "partnership",
                        principalTable: "production_cycles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_returns",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cycle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    return_date = table.Column<DateOnly>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_returns", x => x.id);
                    table.ForeignKey(
                        name: "fk_stock_returns_branches_branch_id",
                        column: x => x.branch_id,
                        principalSchema: "master",
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_returns_production_cycles_cycle_id",
                        column: x => x.cycle_id,
                        principalSchema: "partnership",
                        principalTable: "production_cycles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_returns_warehouses_from_warehouse_id",
                        column: x => x.from_warehouse_id,
                        principalSchema: "master",
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_returns_warehouses_to_warehouse_id",
                        column: x => x.to_warehouse_id,
                        principalSchema: "master",
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "daily_recording_revisions",
                schema: "production",
                columns: table => new
                {
                    daily_recording_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_number = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    previous_values = table.Column<string>(type: "jsonb", nullable: false),
                    revised_by = table.Column<Guid>(type: "uuid", nullable: true),
                    revised_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_daily_recording_revisions", x => new { x.daily_recording_id, x.revision_number });
                    table.ForeignKey(
                        name: "fk_daily_recording_revisions_daily_recordings_daily_recording_",
                        column: x => x.daily_recording_id,
                        principalSchema: "production",
                        principalTable: "daily_recordings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "daily_recording_usages",
                schema: "production",
                columns: table => new
                {
                    daily_recording_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    uom_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    base_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_daily_recording_usages", x => new { x.daily_recording_id, x.item_id });
                    table.ForeignKey(
                        name: "fk_daily_recording_usages_daily_recordings_daily_recording_id",
                        column: x => x.daily_recording_id,
                        principalSchema: "production",
                        principalTable: "daily_recordings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_daily_recording_usages_items_item_id",
                        column: x => x.item_id,
                        principalSchema: "master",
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_daily_recording_usages_uoms_uom_id",
                        column: x => x.uom_id,
                        principalSchema: "master",
                        principalTable: "uoms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_return_lines",
                schema: "inventory",
                columns: table => new
                {
                    stock_return_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    uom_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    base_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_return_lines", x => new { x.stock_return_id, x.line_number });
                    table.ForeignKey(
                        name: "fk_stock_return_lines_items_item_id",
                        column: x => x.item_id,
                        principalSchema: "master",
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_return_lines_stock_returns_stock_return_id",
                        column: x => x.stock_return_id,
                        principalSchema: "inventory",
                        principalTable: "stock_returns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_stock_return_lines_uoms_uom_id",
                        column: x => x.uom_id,
                        principalSchema: "master",
                        principalTable: "uoms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cycle_harvests_cycle_id_date",
                schema: "partnership",
                table: "cycle_harvests",
                columns: new[] { "cycle_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_daily_recording_usages_item_id",
                schema: "production",
                table: "daily_recording_usages",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "ix_daily_recording_usages_uom_id",
                schema: "production",
                table: "daily_recording_usages",
                column: "uom_id");

            migrationBuilder.CreateIndex(
                name: "ix_daily_recordings_branch_id_date",
                schema: "production",
                table: "daily_recordings",
                columns: new[] { "branch_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_daily_recordings_coop_id",
                schema: "production",
                table: "daily_recordings",
                column: "coop_id");

            migrationBuilder.CreateIndex(
                name: "ix_daily_recordings_cycle_id_date",
                schema: "production",
                table: "daily_recordings",
                columns: new[] { "cycle_id", "date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_return_lines_item_id",
                schema: "inventory",
                table: "stock_return_lines",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_return_lines_uom_id",
                schema: "inventory",
                table: "stock_return_lines",
                column: "uom_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_returns_branch_id_return_date",
                schema: "inventory",
                table: "stock_returns",
                columns: new[] { "branch_id", "return_date" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_returns_cycle_id",
                schema: "inventory",
                table: "stock_returns",
                column: "cycle_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_returns_from_warehouse_id",
                schema: "inventory",
                table: "stock_returns",
                column: "from_warehouse_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_returns_number",
                schema: "inventory",
                table: "stock_returns",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_returns_to_warehouse_id",
                schema: "inventory",
                table: "stock_returns",
                column: "to_warehouse_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cycle_harvests",
                schema: "partnership");

            migrationBuilder.DropTable(
                name: "daily_recording_revisions",
                schema: "production");

            migrationBuilder.DropTable(
                name: "daily_recording_usages",
                schema: "production");

            migrationBuilder.DropTable(
                name: "stock_return_lines",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "daily_recordings",
                schema: "production");

            migrationBuilder.DropTable(
                name: "stock_returns",
                schema: "inventory");

            migrationBuilder.DropColumn(
                name: "closed_date",
                schema: "partnership",
                table: "production_cycles");

            migrationBuilder.DropColumn(
                name: "closing_performance",
                schema: "partnership",
                table: "production_cycles");

            migrationBuilder.DropColumn(
                name: "harvested_birds",
                schema: "partnership",
                table: "production_cycles");

            migrationBuilder.DropColumn(
                name: "harvested_weight_kg",
                schema: "partnership",
                table: "production_cycles");

            migrationBuilder.DropColumn(
                name: "total_culling",
                schema: "partnership",
                table: "production_cycles");

            migrationBuilder.DropColumn(
                name: "total_mortality",
                schema: "partnership",
                table: "production_cycles");
        }
    }
}
