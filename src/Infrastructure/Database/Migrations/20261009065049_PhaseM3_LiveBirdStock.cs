using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class PhaseM3_LiveBirdStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "weight_ranges",
                schema: "master",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    min_weight_kg = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: true),
                    max_weight_kg = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_weight_ranges", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "live_bird_stock_entries",
                schema: "production",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cycle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    coop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    age_days = table.Column<int>(type: "integer", nullable: false),
                    weight_range_id = table.Column<Guid>(type: "uuid", nullable: false),
                    birds = table.Column<int>(type: "integer", nullable: false),
                    weight_kg = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_live_bird_stock_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_live_bird_stock_entries_branches_branch_id",
                        column: x => x.branch_id,
                        principalSchema: "master",
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_live_bird_stock_entries_coops_coop_id",
                        column: x => x.coop_id,
                        principalSchema: "master",
                        principalTable: "coops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_live_bird_stock_entries_production_cycles_cycle_id",
                        column: x => x.cycle_id,
                        principalSchema: "partnership",
                        principalTable: "production_cycles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_live_bird_stock_entries_weight_ranges_weight_range_id",
                        column: x => x.weight_range_id,
                        principalSchema: "master",
                        principalTable: "weight_ranges",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_live_bird_stock_entries_branch_id_date",
                schema: "production",
                table: "live_bird_stock_entries",
                columns: new[] { "branch_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_live_bird_stock_entries_coop_id",
                schema: "production",
                table: "live_bird_stock_entries",
                column: "coop_id");

            migrationBuilder.CreateIndex(
                name: "ix_live_bird_stock_entries_cycle_id_date_weight_range_id",
                schema: "production",
                table: "live_bird_stock_entries",
                columns: new[] { "cycle_id", "date", "weight_range_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_live_bird_stock_entries_weight_range_id",
                schema: "production",
                table: "live_bird_stock_entries",
                column: "weight_range_id");

            migrationBuilder.CreateIndex(
                name: "ix_weight_ranges_code",
                schema: "master",
                table: "weight_ranges",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "live_bird_stock_entries",
                schema: "production");

            migrationBuilder.DropTable(
                name: "weight_ranges",
                schema: "master");
        }
    }
}
