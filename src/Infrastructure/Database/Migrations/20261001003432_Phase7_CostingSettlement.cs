using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Phase7_CostingSettlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "costing");

            migrationBuilder.AddColumn<decimal>(
                name: "cost_amount",
                schema: "sales",
                table: "sales_invoice_lines",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "closing_cost",
                schema: "partnership",
                table: "production_cycles",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "vendor_id",
                schema: "finance",
                table: "payment_vouchers",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "farmer_id",
                schema: "finance",
                table: "payment_vouchers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payee_type",
                schema: "finance",
                table: "payment_vouchers",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Vendor");

            migrationBuilder.CreateTable(
                name: "plasma_settlements",
                schema: "costing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cycle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farmer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contract_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scheme = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    settlement_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    income_tax_code_id = table.Column<Guid>(type: "uuid", nullable: true),
                    income_tax_rate_percent = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    debt_deduction = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    deficit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    gross_income = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    income_tax_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    net_payable = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    paid_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plasma_settlements", x => x.id);
                    table.ForeignKey(
                        name: "fk_plasma_settlements_branches_branch_id",
                        column: x => x.branch_id,
                        principalSchema: "master",
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_plasma_settlements_contracts_contract_id",
                        column: x => x.contract_id,
                        principalSchema: "partnership",
                        principalTable: "contracts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_plasma_settlements_farmers_farmer_id",
                        column: x => x.farmer_id,
                        principalSchema: "master",
                        principalTable: "farmers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_plasma_settlements_production_cycles_cycle_id",
                        column: x => x.cycle_id,
                        principalSchema: "partnership",
                        principalTable: "production_cycles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_plasma_settlements_tax_codes_income_tax_code_id",
                        column: x => x.income_tax_code_id,
                        principalSchema: "master",
                        principalTable: "tax_codes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_voucher_settlement_allocations",
                schema: "finance",
                columns: table => new
                {
                    payment_voucher_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plasma_settlement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payment_voucher_settlement_allocations", x => new { x.payment_voucher_id, x.plasma_settlement_id });
                    table.ForeignKey(
                        name: "fk_payment_voucher_settlement_allocations_payment_vouchers_pay",
                        column: x => x.payment_voucher_id,
                        principalSchema: "finance",
                        principalTable: "payment_vouchers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_payment_voucher_settlement_allocations_plasma_settlements_p",
                        column: x => x.plasma_settlement_id,
                        principalSchema: "costing",
                        principalTable: "plasma_settlements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plasma_settlement_lines",
                schema: "costing",
                columns: table => new
                {
                    plasma_settlement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plasma_settlement_lines", x => new { x.plasma_settlement_id, x.line_number });
                    table.ForeignKey(
                        name: "fk_plasma_settlement_lines_plasma_settlements_plasma_settlemen",
                        column: x => x.plasma_settlement_id,
                        principalSchema: "costing",
                        principalTable: "plasma_settlements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_payment_vouchers_farmer_id_status",
                schema: "finance",
                table: "payment_vouchers",
                columns: new[] { "farmer_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_payment_voucher_settlement_allocations_plasma_settlement_id",
                schema: "finance",
                table: "payment_voucher_settlement_allocations",
                column: "plasma_settlement_id");

            migrationBuilder.CreateIndex(
                name: "ix_plasma_settlements_branch_id_settlement_date",
                schema: "costing",
                table: "plasma_settlements",
                columns: new[] { "branch_id", "settlement_date" });

            migrationBuilder.CreateIndex(
                name: "ix_plasma_settlements_contract_id",
                schema: "costing",
                table: "plasma_settlements",
                column: "contract_id");

            migrationBuilder.CreateIndex(
                name: "ix_plasma_settlements_cycle_id_active",
                schema: "costing",
                table: "plasma_settlements",
                column: "cycle_id",
                unique: true,
                filter: "status <> 'Cancelled'");

            migrationBuilder.CreateIndex(
                name: "ix_plasma_settlements_farmer_id_status",
                schema: "costing",
                table: "plasma_settlements",
                columns: new[] { "farmer_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_plasma_settlements_income_tax_code_id",
                schema: "costing",
                table: "plasma_settlements",
                column: "income_tax_code_id");

            migrationBuilder.CreateIndex(
                name: "ix_plasma_settlements_number",
                schema: "costing",
                table: "plasma_settlements",
                column: "number",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_payment_vouchers_farmers_farmer_id",
                schema: "finance",
                table: "payment_vouchers",
                column: "farmer_id",
                principalSchema: "master",
                principalTable: "farmers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_payment_vouchers_farmers_farmer_id",
                schema: "finance",
                table: "payment_vouchers");

            migrationBuilder.DropTable(
                name: "payment_voucher_settlement_allocations",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "plasma_settlement_lines",
                schema: "costing");

            migrationBuilder.DropTable(
                name: "plasma_settlements",
                schema: "costing");

            migrationBuilder.DropIndex(
                name: "ix_payment_vouchers_farmer_id_status",
                schema: "finance",
                table: "payment_vouchers");

            migrationBuilder.DropColumn(
                name: "cost_amount",
                schema: "sales",
                table: "sales_invoice_lines");

            migrationBuilder.DropColumn(
                name: "closing_cost",
                schema: "partnership",
                table: "production_cycles");

            migrationBuilder.DropColumn(
                name: "farmer_id",
                schema: "finance",
                table: "payment_vouchers");

            migrationBuilder.DropColumn(
                name: "payee_type",
                schema: "finance",
                table: "payment_vouchers");

            migrationBuilder.AlterColumn<Guid>(
                name: "vendor_id",
                schema: "finance",
                table: "payment_vouchers",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
