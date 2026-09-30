using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Phase5_SalesReceivables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "sales");

            migrationBuilder.CreateTable(
                name: "customer_receipts",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt_date = table.Column<DateOnly>(type: "date", nullable: false),
                    cash_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_receipts", x => x.id);
                    table.ForeignKey(
                        name: "fk_customer_receipts_accounts_cash_account_id",
                        column: x => x.cash_account_id,
                        principalSchema: "finance",
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_customer_receipts_branches_branch_id",
                        column: x => x.branch_id,
                        principalSchema: "master",
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_customer_receipts_customers_customer_id",
                        column: x => x.customer_id,
                        principalSchema: "master",
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_invoices",
                schema: "sales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_date = table.Column<DateOnly>(type: "date", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    posted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    posted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    paid_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales_invoices", x => x.id);
                    table.ForeignKey(
                        name: "fk_sales_invoices_branches_branch_id",
                        column: x => x.branch_id,
                        principalSchema: "master",
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sales_invoices_customers_customer_id",
                        column: x => x.customer_id,
                        principalSchema: "master",
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_orders",
                schema: "sales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_date = table.Column<DateOnly>(type: "date", nullable: false),
                    delivery_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    credit_override_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales_orders", x => x.id);
                    table.ForeignKey(
                        name: "fk_sales_orders_branches_branch_id",
                        column: x => x.branch_id,
                        principalSchema: "master",
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sales_orders_customers_customer_id",
                        column: x => x.customer_id,
                        principalSchema: "master",
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "customer_receipt_allocations",
                schema: "finance",
                columns: table => new
                {
                    customer_receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_receipt_allocations", x => new { x.customer_receipt_id, x.sales_invoice_id });
                    table.ForeignKey(
                        name: "fk_customer_receipt_allocations_customer_receipts_customer_rec",
                        column: x => x.customer_receipt_id,
                        principalSchema: "finance",
                        principalTable: "customer_receipts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_customer_receipt_allocations_sales_invoices_sales_invoice_id",
                        column: x => x.sales_invoice_id,
                        principalSchema: "sales",
                        principalTable: "sales_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "delivery_orders",
                schema: "sales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delivery_date = table.Column<DateOnly>(type: "date", nullable: false),
                    vehicle_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    driver_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    sales_invoice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_delivery_orders", x => x.id);
                    table.ForeignKey(
                        name: "fk_delivery_orders_branches_branch_id",
                        column: x => x.branch_id,
                        principalSchema: "master",
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_delivery_orders_customers_customer_id",
                        column: x => x.customer_id,
                        principalSchema: "master",
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_delivery_orders_sales_invoices_sales_invoice_id",
                        column: x => x.sales_invoice_id,
                        principalSchema: "sales",
                        principalTable: "sales_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_delivery_orders_sales_orders_sales_order_id",
                        column: x => x.sales_order_id,
                        principalSchema: "sales",
                        principalTable: "sales_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_order_lines",
                schema: "sales",
                columns: table => new
                {
                    sales_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    birds = table.Column<int>(type: "integer", nullable: false),
                    estimated_weight_kg = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    tax_code_id = table.Column<Guid>(type: "uuid", nullable: true),
                    delivered_birds = table.Column<int>(type: "integer", nullable: false),
                    delivered_weight_kg = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    price_per_kg = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales_order_lines", x => new { x.sales_order_id, x.line_number });
                    table.ForeignKey(
                        name: "fk_sales_order_lines_items_item_id",
                        column: x => x.item_id,
                        principalSchema: "master",
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sales_order_lines_sales_orders_sales_order_id",
                        column: x => x.sales_order_id,
                        principalSchema: "sales",
                        principalTable: "sales_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sales_order_lines_tax_codes_tax_code_id",
                        column: x => x.tax_code_id,
                        principalSchema: "master",
                        principalTable: "tax_codes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "delivery_order_lines",
                schema: "sales",
                columns: table => new
                {
                    delivery_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    sales_order_line_number = table.Column<int>(type: "integer", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    harvest_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cycle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    birds = table.Column<int>(type: "integer", nullable: false),
                    weight_kg = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    tax_code_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_cancelled = table.Column<bool>(type: "boolean", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    price_per_kg = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_delivery_order_lines", x => new { x.delivery_order_id, x.line_number });
                    table.ForeignKey(
                        name: "fk_delivery_order_lines_cycle_harvests_harvest_id",
                        column: x => x.harvest_id,
                        principalSchema: "partnership",
                        principalTable: "cycle_harvests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_delivery_order_lines_delivery_orders_delivery_order_id",
                        column: x => x.delivery_order_id,
                        principalSchema: "sales",
                        principalTable: "delivery_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_delivery_order_lines_items_item_id",
                        column: x => x.item_id,
                        principalSchema: "master",
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_delivery_order_lines_production_cycles_cycle_id",
                        column: x => x.cycle_id,
                        principalSchema: "partnership",
                        principalTable: "production_cycles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_delivery_order_lines_tax_codes_tax_code_id",
                        column: x => x.tax_code_id,
                        principalSchema: "master",
                        principalTable: "tax_codes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_invoice_lines",
                schema: "sales",
                columns: table => new
                {
                    sales_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    delivery_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delivery_order_line_number = table.Column<int>(type: "integer", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cycle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    birds = table.Column<int>(type: "integer", nullable: false),
                    weight_kg = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    tax_code_id = table.Column<Guid>(type: "uuid", nullable: true),
                    vat_rate_percent = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    price_per_kg = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vat_tax_base = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales_invoice_lines", x => new { x.sales_invoice_id, x.line_number });
                    table.ForeignKey(
                        name: "fk_sales_invoice_lines_delivery_orders_delivery_order_id",
                        column: x => x.delivery_order_id,
                        principalSchema: "sales",
                        principalTable: "delivery_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sales_invoice_lines_items_item_id",
                        column: x => x.item_id,
                        principalSchema: "master",
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sales_invoice_lines_production_cycles_cycle_id",
                        column: x => x.cycle_id,
                        principalSchema: "partnership",
                        principalTable: "production_cycles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sales_invoice_lines_sales_invoices_sales_invoice_id",
                        column: x => x.sales_invoice_id,
                        principalSchema: "sales",
                        principalTable: "sales_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sales_invoice_lines_tax_codes_tax_code_id",
                        column: x => x.tax_code_id,
                        principalSchema: "master",
                        principalTable: "tax_codes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_customer_receipt_allocations_sales_invoice_id",
                schema: "finance",
                table: "customer_receipt_allocations",
                column: "sales_invoice_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_receipts_branch_id_receipt_date",
                schema: "finance",
                table: "customer_receipts",
                columns: new[] { "branch_id", "receipt_date" });

            migrationBuilder.CreateIndex(
                name: "ix_customer_receipts_cash_account_id",
                schema: "finance",
                table: "customer_receipts",
                column: "cash_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_receipts_customer_id_receipt_date",
                schema: "finance",
                table: "customer_receipts",
                columns: new[] { "customer_id", "receipt_date" });

            migrationBuilder.CreateIndex(
                name: "ix_customer_receipts_number",
                schema: "finance",
                table: "customer_receipts",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_delivery_order_lines_cycle_id",
                schema: "sales",
                table: "delivery_order_lines",
                column: "cycle_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_order_lines_harvest_id_active",
                schema: "sales",
                table: "delivery_order_lines",
                column: "harvest_id",
                unique: true,
                filter: "is_cancelled = false");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_order_lines_item_id",
                schema: "sales",
                table: "delivery_order_lines",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_order_lines_tax_code_id",
                schema: "sales",
                table: "delivery_order_lines",
                column: "tax_code_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_orders_branch_id_delivery_date",
                schema: "sales",
                table: "delivery_orders",
                columns: new[] { "branch_id", "delivery_date" });

            migrationBuilder.CreateIndex(
                name: "ix_delivery_orders_customer_id_status",
                schema: "sales",
                table: "delivery_orders",
                columns: new[] { "customer_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_delivery_orders_number",
                schema: "sales",
                table: "delivery_orders",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_delivery_orders_sales_invoice_id",
                schema: "sales",
                table: "delivery_orders",
                column: "sales_invoice_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_orders_sales_order_id",
                schema: "sales",
                table: "delivery_orders",
                column: "sales_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoice_lines_cycle_id",
                schema: "sales",
                table: "sales_invoice_lines",
                column: "cycle_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoice_lines_delivery_order_id",
                schema: "sales",
                table: "sales_invoice_lines",
                column: "delivery_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoice_lines_item_id",
                schema: "sales",
                table: "sales_invoice_lines",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoice_lines_tax_code_id",
                schema: "sales",
                table: "sales_invoice_lines",
                column: "tax_code_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoices_branch_id_invoice_date",
                schema: "sales",
                table: "sales_invoices",
                columns: new[] { "branch_id", "invoice_date" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoices_customer_id_status",
                schema: "sales",
                table: "sales_invoices",
                columns: new[] { "customer_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoices_number",
                schema: "sales",
                table: "sales_invoices",
                column: "number",
                unique: true,
                filter: "number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_sales_order_lines_item_id",
                schema: "sales",
                table: "sales_order_lines",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_order_lines_tax_code_id",
                schema: "sales",
                table: "sales_order_lines",
                column: "tax_code_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_orders_branch_id_order_date",
                schema: "sales",
                table: "sales_orders",
                columns: new[] { "branch_id", "order_date" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_orders_customer_id_status",
                schema: "sales",
                table: "sales_orders",
                columns: new[] { "customer_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_orders_number",
                schema: "sales",
                table: "sales_orders",
                column: "number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_receipt_allocations",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "delivery_order_lines",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "sales_invoice_lines",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "sales_order_lines",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "customer_receipts",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "delivery_orders",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "sales_invoices",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "sales_orders",
                schema: "sales");
        }
    }
}
